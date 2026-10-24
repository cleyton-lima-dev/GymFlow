using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

// Synthetic App-only tenants exercise the production HTTP pipeline and repositories.
// Financial setup is deliberately absent from every compatibility scenario.
public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    public Task G6_AppOnly_LegacyIdentityAndOperationalHistorySurviveLifecycle(string mode) =>
        CheckG6LegacyCompatibility(false, mode);

    [PostgresTheory]
    [InlineData("missing")]
    [InlineData("disabled")]
    public Task PostgreSql_G6_AppOnly_LegacyIdentityAndOperationalHistorySurviveLifecycle(string mode) =>
        CheckG6LegacyCompatibility(true, mode);

    private static async Task CheckG6LegacyCompatibility(bool postgres, string mode)
    {
        await using var factory = new TestApi(postgres);
        var gym = mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var professor = factory.AddUser(gym, UserRole.Professor);
        var user = factory.AddUser(gym);
        var data = SeedG6TraditionalRecords(factory, user);

        // This would archive an ERP student. App-only lifecycle must leave all data untouched.
        await ProcessG6Lifecycle(factory, gym);
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            user.Email, Password = TestApi.Password,
            GymId = factory.EnabledGym, ErpEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var identity = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Id, identity.GetProperty("userId").GetGuid());
        Assert.Equal(gym, identity.GetProperty("gymId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new("Bearer", identity.GetProperty("token").GetString());

        var me = await ReadG6Json(client, "/api/students/me");
        Assert.Equal(data.StudentId, me.GetProperty("id").GetGuid());
        Assert.Equal(user.Id, me.GetProperty("userId").GetGuid());
        Assert.True(me.GetProperty("isActive").GetBoolean());
        Assert.True(me.GetProperty("appAccessEnabled").GetBoolean());
        Assert.Equal(user.Id, (await ReadG6Json(client, "/api/auth/me")).GetProperty("userId").GetGuid());
        using (var studentFeatureResponse = await client.GetAsync("/api/gym-features"))
            Assert.Equal(HttpStatusCode.Forbidden, studentFeatureResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.False((await ReadG6Json(client, "/api/gym-features")).GetProperty("erpEnabled").GetBoolean());

        client.DefaultRequestHeaders.Authorization = new("Bearer", identity.GetProperty("token").GetString());
        var workout = await ReadG6Json(client, "/api/workouts/me/current");
        Assert.Equal(data.WorkoutId, workout.GetProperty("id").GetGuid());
        Assert.Equal(data.StudentId, workout.GetProperty("studentId").GetGuid());
        Assert.Equal(data.DayId, workout.GetProperty("days")[0].GetProperty("id").GetGuid());
        Assert.Equal(data.WorkoutExerciseId,
            workout.GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("id").GetGuid());
        var history = await ReadG6Json(client, "/api/workouts/me/history");
        Assert.Equal(data.ExecutionId, Assert.Single(history.GetProperty("items").EnumerateArray()).GetProperty("executionId").GetGuid());
        Assert.Equal(data.HistoricWorkoutId, history.GetProperty("items")[0].GetProperty("workoutId").GetGuid());

        var assessment = await ReadG6Json(client, "/api/physical-assessments/me/latest");
        Assert.Equal(data.AssessmentId, assessment.GetProperty("id").GetGuid());
        Assert.Equal(75m, assessment.GetProperty("weightKg").GetDecimal());
        Assert.Equal(data.AssessmentId,
            (await ReadG6Json(client, $"/api/physical-assessments/me/{data.AssessmentId}")).GetProperty("id").GetGuid());
        Assert.Equal(2, (await ReadG6Json(client, "/api/physical-assessments/me")).GetProperty("totalCount").GetInt32());

        foreach (var staff in new[] { admin, professor })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(staff));
            var students = await ReadG6Json(client, $"/api/students?gymId={factory.EnabledGym}");
            Assert.Equal(data.StudentId, Assert.Single(students.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            Assert.Equal(data.StudentId,
                (await ReadG6Json(client, $"/api/students/{data.StudentId}")).GetProperty("id").GetGuid());
            Assert.Equal(data.WorkoutId,
                (await ReadG6Json(client, $"/api/workouts/students/{data.StudentId}/current")).GetProperty("id").GetGuid());
            Assert.Equal(data.ExecutionId,
                (await ReadG6Json(client, $"/api/workouts/students/{data.StudentId}/history")).GetProperty("items")[0].GetProperty("executionId").GetGuid());
            Assert.Equal(data.AssessmentId,
                (await ReadG6Json(client, $"/api/students/{data.StudentId}/physical-assessments/latest")).GetProperty("id").GetGuid());
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var operationalDashboard = await ReadG6Json(client, "/api/dashboard/operational");
        Assert.Equal(1, operationalDashboard.GetProperty("activeStudents").GetInt32());
        Assert.Equal(0, operationalDashboard.GetProperty("studentsWithoutAssessment").GetInt32());
        Assert.Equal(1, operationalDashboard.GetProperty("reassessmentsDue").GetInt32());
        Assert.True(operationalDashboard.TryGetProperty("today", out _));
        Assert.Equal(4, operationalDashboard.EnumerateObject().Count());

        await ProcessG6Lifecycle(factory, gym);
        factory.WithDb(db =>
        {
            Assert.Empty(db.Plans);
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.FinancialAuditLogs);
            var persistedUser = db.Users.Single(x => x.Id == user.Id);
            Assert.True(persistedUser.IsActive);
            Assert.Equal(user.PasswordHash, persistedUser.PasswordHash);
            Assert.Equal(user.Email, persistedUser.Email);
            var student = db.Students.Single(x => x.Id == data.StudentId);
            Assert.Equal(user.Id, student.UserId);
            Assert.Equal(gym, student.GymId);
            Assert.True(student.IsActive);
            Assert.True(student.AppAccessEnabled);
            Assert.Null(student.ArchivedAt);
            Assert.Null(student.InactivationReason);
            Assert.Null(student.InactivatedAt);
            Assert.Equal(new DateOnly(2024, 1, 1), student.NoValidEnrollmentSince);
            Assert.Null(student.UpdatedAt);
            Assert.Equal(2, db.Workouts.Count());
            Assert.True(db.Workouts.Single(x => x.Id == data.WorkoutId).IsActive);
            Assert.False(db.Workouts.Single(x => x.Id == data.HistoricWorkoutId).IsActive);
            Assert.Equal(data.DayId, db.WorkoutExercises.Single(x => x.Id == data.WorkoutExerciseId).WorkoutDayId);
            Assert.Equal(data.ExerciseId, db.WorkoutExercises.Single(x => x.Id == data.WorkoutExerciseId).ExerciseId);
            Assert.Equal(data.HistoricDayId, db.WorkoutExecutions.Single(x => x.Id == data.ExecutionId).WorkoutDayId);
            Assert.Equal(G6HistoricTimestamp, db.WorkoutExecutions.Single(x => x.Id == data.ExecutionId).CompletedAt);
            Assert.Equal(2, db.PhysicalAssessments.Count());
            Assert.Equal("Preserved synthetic assessment", db.PhysicalAssessments.Single(x => x.Id == data.AssessmentId).Notes);
            Assert.Equal(data.StudentId, db.PhysicalAssessments.Single(x => x.Id == data.OldAssessmentId).StudentId);
            Assert.Single(db.Exercises);
            Assert.Single(db.WorkoutTemplates);
            Assert.Empty(db.WorkoutDayProgresses);
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    public Task G6_AppOnly_AdministrativeAndTrainingWritesNeedNoFinancialSetup(string mode) =>
        CheckG6TraditionalWrites(false, mode);

    [PostgresTheory]
    [InlineData("missing")]
    [InlineData("disabled")]
    public Task PostgreSql_G6_AppOnly_AdministrativeAndTrainingWritesNeedNoFinancialSetup(string mode) =>
        CheckG6TraditionalWrites(true, mode);

    private static async Task CheckG6TraditionalWrites(bool postgres, string mode)
    {
        await using var factory = new TestApi(postgres);
        var gym = mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var professorEmail = $"g6-professor-{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new
        {
            Name = "G6 Professor", Email = professorEmail, Password = TestApi.Password,
            GymId = factory.EnabledGym, Role = "Admin"
        })).StatusCode);
        var professors = await ReadG6Json(client, "/api/auth/professors");
        var professorId = Assert.Single(professors.EnumerateArray()).GetProperty("id").GetGuid();

        var studentEmail = $"g6-student-{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/students", new
        {
            Name = "G6 Administrative Student", Email = studentEmail, GymId = factory.EnabledGym
        })).StatusCode);
        var students = await ReadG6Json(client, "/api/students");
        var studentId = Assert.Single(students.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/students/{studentId}", new
            { Name = "G6 Updated Student", Email = studentEmail, Phone = "11999999999" })).StatusCode);
        foreach (var active in new[] { false, true })
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PatchAsJsonAsync($"/api/students/{studentId}/status", new { IsActive = active })).StatusCode);
        var invite = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access", new { Enabled = true });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var inviteToken = (await invite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(inviteToken));
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/students/app-activate/validate", new { Token = inviteToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/students/app-activate", new { Token = inviteToken, Password = TestApi.Password })).StatusCode);

        var professorLogin = await client.PostAsJsonAsync("/api/auth/login", new { Email = professorEmail, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, professorLogin.StatusCode);
        var professorIdentity = await professorLogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(gym, professorIdentity.GetProperty("gymId").GetGuid());
        Assert.Equal("Professor", professorIdentity.GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new("Bearer", professorIdentity.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/professors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/students/{studentId}", new { Name = "Forbidden", Email = studentEmail })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/exercises", new { Name = "G6 Exercise", MuscleGroup = "Legs" })).StatusCode);
        var exerciseId = Assert.Single((await ReadG6Json(client, "/api/exercises")).GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/exercises/{exerciseId}", new { Name = "G6 Updated Exercise", MuscleGroup = "Legs" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PatchAsJsonAsync($"/api/exercises/{exerciseId}/status", new { IsActive = false })).StatusCode);
        var days = G6TrainingDays(exerciseId);
        var templateResponse = await client.PostAsJsonAsync("/api/workout-templates", new { Name = "G6 Template", Days = days });
        Assert.Equal(HttpStatusCode.Created, templateResponse.StatusCode);
        var templateId = (await templateResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/workout-templates/{templateId}/status", new { IsActive = true })).StatusCode);
        var workoutResponse = await client.PostAsJsonAsync("/api/workouts/from-template", new
        { StudentId = studentId, TemplateId = templateId, Name = "G6 Assigned Workout", Days = days });
        Assert.Equal(HttpStatusCode.Created, workoutResponse.StatusCode);
        var workoutId = (await workoutResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync($"/api/students/{studentId}/physical-assessments", new
            { AssessmentDate = today, WeightKg = 75, HeightCm = 175, Notes = "G6 Created" })).StatusCode);
        var assessmentId = (await ReadG6Json(client, $"/api/students/{studentId}/physical-assessments/latest")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/students/{studentId}/physical-assessments/{assessmentId}", new
            { WeightKg = 76, HeightCm = 175, Notes = "G6 Updated" })).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var studentLogin = await client.PostAsJsonAsync("/api/auth/login", new { Email = studentEmail, Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.OK, studentLogin.StatusCode);
        var studentIdentity = await studentLogin.Content.ReadFromJsonAsync<JsonElement>();
        var userId = studentIdentity.GetProperty("userId").GetGuid();
        Assert.Equal(gym, studentIdentity.GetProperty("gymId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new("Bearer", studentIdentity.GetProperty("token").GetString());
        Assert.Equal(studentId, (await ReadG6Json(client, "/api/students/me")).GetProperty("id").GetGuid());
        var workout = await ReadG6Json(client, "/api/workouts/me/current");
        Assert.Equal(workoutId, workout.GetProperty("id").GetGuid());
        var dayId = workout.GetProperty("days")[0].GetProperty("id").GetGuid();
        var workoutExerciseId = workout.GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/workouts/me/days/{dayId}/exercises/{workoutExerciseId}/complete", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/workouts/me/days/{dayId}/complete", new { })).StatusCode);
        Assert.Equal(workoutId,
            (await ReadG6Json(client, "/api/workouts/me/history")).GetProperty("items")[0].GetProperty("workoutId").GetGuid());
        Assert.Equal(76m, (await ReadG6Json(client, "/api/physical-assessments/me/latest")).GetProperty("weightKg").GetDecimal());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync("/api/gym-settings", new { AdministrativeName = "G6 Shared Settings", TimeZoneId = "UTC" })).StatusCode);
        Assert.False((await ReadG6Json(client, "/api/gym-features")).GetProperty("erpEnabled").GetBoolean());
        await ProcessG6Lifecycle(factory, gym);
        factory.WithDb(db =>
        {
            Assert.Empty(db.Plans);
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.FinancialAuditLogs);
            var student = db.Students.Single(x => x.Id == studentId);
            Assert.Equal(userId, student.UserId);
            Assert.True(student.IsActive);
            Assert.Null(student.ArchivedAt);
            Assert.Equal(gym, student.GymId);
            Assert.Equal(UserRole.Professor, db.Users.Single(x => x.Id == professorId).Role);
            Assert.Equal(gym, db.Users.Single(x => x.Id == professorId).GymId);
            Assert.Equal(gym, db.Exercises.Single(x => x.Id == exerciseId).GymId);
            Assert.Equal(gym, db.Workouts.Single(x => x.Id == workoutId).GymId);
            Assert.Equal(gym, db.WorkoutTemplates.Single(x => x.Id == templateId).GymId);
            Assert.Equal(gym, Assert.Single(db.GymSettings).GymId);
        });
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Professor)]
    public Task G6_TraditionalStaffEndpoints_IsolateForeignStudentsAndTraining(UserRole role) =>
        CheckG6TraditionalTenantIsolation(false, role);

    [PostgresTheory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Professor)]
    public Task PostgreSql_G6_TraditionalStaffEndpoints_IsolateForeignStudentsAndTraining(UserRole role) =>
        CheckG6TraditionalTenantIsolation(true, role);

    private static async Task CheckG6TraditionalTenantIsolation(bool postgres, UserRole role)
    {
        await using var factory = new TestApi(postgres);
        var staff = factory.AddUser(factory.LegacyGym, role);
        var own = SeedG6TraditionalRecords(factory, factory.AddUser(factory.LegacyGym));
        var foreign = SeedG6TraditionalRecords(factory, factory.AddUser(factory.DisabledGym));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(staff));
        foreach (var endpoint in new[]
        {
            $"/api/students/{foreign.StudentId}",
            $"/api/workouts/students/{foreign.StudentId}/current",
            $"/api/workouts/students/{foreign.StudentId}/history",
            $"/api/workout-templates/{foreign.TemplateId}",
            $"/api/students/{foreign.StudentId}/physical-assessments",
            $"/api/students/{foreign.StudentId}/physical-assessments/latest",
            $"/api/students/{foreign.StudentId}/physical-assessments/{foreign.AssessmentId}",
            $"/api/students/{own.StudentId}/physical-assessments/{foreign.AssessmentId}"
        })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(endpoint)).StatusCode);
        foreach (var endpoint in new[] { "/api/students", "/api/exercises", "/api/workout-templates" })
        {
            var list = await ReadG6Json(client, $"{endpoint}?gymId={factory.DisabledGym}");
            Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
            var itemId = list.GetProperty("items")[0].GetProperty("id").GetGuid();
            Assert.Equal(endpoint == "/api/students" ? own.StudentId : endpoint == "/api/exercises" ? own.ExerciseId : own.TemplateId, itemId);
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/workouts", new { StudentId = foreign.StudentId, Name = "Forbidden", Days = G6TrainingDays(own.ExerciseId) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workouts", new { StudentId = own.StudentId, Name = "Forbidden", Days = G6TrainingDays(foreign.ExerciseId) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/workouts/from-template", new { StudentId = own.StudentId, TemplateId = foreign.TemplateId, Name = "Forbidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/workouts/{foreign.WorkoutId}", new { Name = "Forbidden", Days = G6TrainingDays(own.ExerciseId) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/workout-templates/{foreign.TemplateId}", new { Name = "Forbidden", Days = G6TrainingDays(own.ExerciseId) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PatchAsJsonAsync($"/api/workout-templates/{foreign.TemplateId}/status", new { IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workout-templates", new { Name = "Forbidden", Days = G6TrainingDays(foreign.ExerciseId) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PutAsJsonAsync($"/api/exercises/{foreign.ExerciseId}", new { Name = "Forbidden", MuscleGroup = "Legs" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/students/{foreign.StudentId}/physical-assessments", new
            { AssessmentDate = DateOnly.FromDateTime(DateTime.UtcNow), WeightKg = 80, HeightCm = 180 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/students/{foreign.StudentId}/physical-assessments/{foreign.AssessmentId}", new
            { WeightKg = 80, HeightCm = 180 })).StatusCode);
        if (role == UserRole.Admin)
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PutAsJsonAsync($"/api/students/{foreign.StudentId}", new { Name = "Forbidden" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PatchAsJsonAsync($"/api/students/{foreign.StudentId}/status", new { IsActive = false })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsJsonAsync($"/api/students/{foreign.StudentId}/app-access", new { Enabled = false })).StatusCode);
        }
        factory.WithDb(db =>
        {
            Assert.True(db.Students.Single(x => x.Id == foreign.StudentId).IsActive);
            Assert.True(db.Students.Single(x => x.Id == foreign.StudentId).AppAccessEnabled);
            Assert.True(db.Workouts.Single(x => x.Id == own.WorkoutId).IsActive);
            Assert.True(db.Workouts.Single(x => x.Id == foreign.WorkoutId).IsActive);
            Assert.Equal(4, db.Workouts.Count());
            Assert.Equal(2, db.WorkoutTemplates.Count());
            Assert.Equal(2, db.Exercises.Count());
            Assert.StartsWith("G6 Existing Exercise", db.Exercises.Single(x => x.Id == foreign.ExerciseId).Name);
            Assert.Equal(75m, db.PhysicalAssessments.Single(x => x.Id == foreign.AssessmentId).WeightKg);
            Assert.Empty(db.Enrollments);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task G6_StudentSelfEndpoints_DoNotAcceptOtherStudentIds(bool sameGym) =>
        CheckG6StudentSelfIsolation(false, sameGym);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PostgreSql_G6_StudentSelfEndpoints_DoNotAcceptOtherStudentIds(bool sameGym) =>
        CheckG6StudentSelfIsolation(true, sameGym);

    private static async Task CheckG6StudentSelfIsolation(bool postgres, bool sameGym)
    {
        await using var factory = new TestApi(postgres);
        var user = factory.AddUser(factory.LegacyGym);
        var own = SeedG6TraditionalRecords(factory, user);
        var other = SeedG6TraditionalRecords(factory, factory.AddUser(sameGym ? factory.LegacyGym : factory.DisabledGym));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        Assert.Equal(own.StudentId,
            (await ReadG6Json(client, $"/api/students/me?studentId={other.StudentId}&gymId={factory.DisabledGym}")).GetProperty("id").GetGuid());
        Assert.Equal(own.WorkoutId,
            (await ReadG6Json(client, $"/api/workouts/me/current?studentId={other.StudentId}")).GetProperty("id").GetGuid());
        Assert.Equal(own.ExecutionId,
            (await ReadG6Json(client, $"/api/workouts/me/history?studentId={other.StudentId}")).GetProperty("items")[0].GetProperty("executionId").GetGuid());
        Assert.Equal(own.AssessmentId,
            (await ReadG6Json(client, $"/api/physical-assessments/me/latest?studentId={other.StudentId}")).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/physical-assessments/me/{other.AssessmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/students/{other.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/workouts/students/{other.StudentId}/current")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/workouts/me/days/{other.DayId}/complete", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/workouts/me/days/{other.DayId}/exercises/{other.WorkoutExerciseId}/complete", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/workouts/me/days/{other.DayId}/exercises/{other.WorkoutExerciseId}/complete")).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Empty(db.WorkoutDayProgresses);
            Assert.Equal(2, db.WorkoutExecutions.Count());
            Assert.Equal(4, db.PhysicalAssessments.Count());
            Assert.Equal(other.StudentId, db.Workouts.Single(x => x.Id == other.WorkoutId).StudentId);
            Assert.Empty(db.Enrollments);
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("enabled")]
    public Task G6_PhysicalAccess_AppliesFinancialPolicyOnlyToErpAndPreservesPhysicalChecks(string mode) =>
        CheckG6PhysicalCompatibility(false, mode);

    [PostgresTheory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("enabled")]
    public Task PostgreSql_G6_PhysicalAccess_AppliesFinancialPolicyOnlyToErpAndPreservesPhysicalChecks(string mode) =>
        CheckG6PhysicalCompatibility(true, mode);

    private static async Task CheckG6PhysicalCompatibility(bool postgres, string mode)
    {
        await using var factory = new TestApi(postgres);
        var gym = mode == "enabled" ? factory.EnabledGym : mode == "disabled" ? factory.DisabledGym : factory.LegacyGym;
        var admin = factory.AddUser(gym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        var foreignStudentId = Guid.NewGuid();
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = gym, Name = "G6 Shared Physical Agent",
            SecretHash = "unused-synthetic", IsActive = true,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-a\",\"providerKey\":\"synthetic\",\"enabled\":true}]"
        };
        factory.WithDb(db =>
        {
            // Physical credentials do not require a digital User or AppAccessEnabled.
            db.Students.Add(new Student { Id = studentId, GymId = gym, Name = "G6 Physical Student", IsActive = true });
            db.Students.Add(new Student { Id = foreignStudentId, GymId = factory.OtherEnabledGym, Name = "G6 Foreign Physical Student", IsActive = true });
            db.PhysicalAccessCredentials.Add(new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(), GymId = factory.OtherEnabledGym, StudentId = foreignStudentId,
                ProviderKey = "synthetic", Type = PhysicalAccessCredentialType.Card,
                ExternalIdentifier = "g6-foreign-card", IsActive = true
            });
            db.AccessAgents.Add(agent);
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var createCredential = await client.PostAsJsonAsync("/api/physical-access/credentials", new
        {
            StudentId = studentId, ProviderKey = "synthetic", Type = PhysicalAccessCredentialType.Card,
            ExternalIdentifier = "g6-own-card", GymId = factory.OtherEnabledGym
        });
        Assert.Equal(HttpStatusCode.Created, createCredential.StatusCode);
        var credentialId = (await createCredential.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var expected = mode == "enabled" ? PhysicalAccessDecision.Denied : PhysicalAccessDecision.Allowed;
        var expectedReason = mode == "enabled" ? PhysicalAccessDecisionReason.NoValidEnrollment : PhysicalAccessDecisionReason.Eligible;

        async Task AssertDecision(string route, string identifier, PhysicalAccessDecision decision, PhysicalAccessDecisionReason reason)
        {
            var response = await client.PostAsJsonAsync(route, new
            {
                RequestId = Guid.NewGuid(), ProviderKey = "synthetic", CredentialType = PhysicalAccessCredentialType.Card,
                ExternalIdentifier = identifier, OccurredAt = DateTime.UtcNow,
                GymId = factory.OtherEnabledGym, StudentId = foreignStudentId, ErpEnabled = false,
                DeviceKey = "door-a"
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<PhysicalAccessDecisionResponse>())!;
            Assert.Equal(decision, result.Decision);
            Assert.Equal(reason, result.Reason);
            if (identifier == "g6-own-card")
            {
                Assert.Equal(studentId, result.StudentId);
                Assert.Equal(credentialId, result.CredentialId);
            }
            else Assert.Null(result.StudentId);
        }

        await AssertDecision("/api/physical-access/decisions", "g6-own-card", expected, expectedReason);
        await AssertDecision("/api/physical-access/decisions", "g6-foreign-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.CredentialNotFound);
        using (var scope = factory.Services.CreateScope())
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        await AssertDecision("/api/access-agent/physical-access/decisions", "g6-own-card", expected, expectedReason);
        await AssertDecision("/api/access-agent/physical-access/decisions", "g6-foreign-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.CredentialNotFound);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/physical-access/credentials/{credentialId}/status", new { IsActive = false })).StatusCode);
        await AssertDecision("/api/physical-access/decisions", "g6-own-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.CredentialInactive);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/physical-access/credentials/{credentialId}/status", new { IsActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/physical-access/overrides/student/{studentId}", new
            { Type = PhysicalAccessOverrideType.Block, Reason = "G6 Physical Block" })).StatusCode);
        await AssertDecision("/api/physical-access/decisions", "g6-own-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.ManualBlock);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/physical-access/overrides/student/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/students/{studentId}/status", new { IsActive = false })).StatusCode);
        await AssertDecision("/api/physical-access/decisions", "g6-own-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.StudentInactive);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/students/{studentId}/status", new { IsActive = true })).StatusCode);
        factory.WithDb(db => db.Students.Single(x => x.Id == studentId).ArchivedAt = DateTime.UtcNow);
        await AssertDecision("/api/physical-access/decisions", "g6-own-card", PhysicalAccessDecision.Denied, PhysicalAccessDecisionReason.StudentArchived);
        factory.WithDb(db =>
        {
            Assert.Empty(db.Plans);
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.CheckIns); // Authorization does not claim confirmed passage.
            var student = db.Students.Single(x => x.Id == studentId);
            Assert.Null(student.UserId);
            Assert.False(student.AppAccessEnabled);
            Assert.Equal(8, db.PhysicalAccessEvents.Count());
            Assert.All(db.PhysicalAccessEvents, accessEvent => Assert.Equal(gym, accessEvent.GymId));
        });
    }

    private static readonly DateTime G6HistoricTimestamp = new(2025, 3, 10, 12, 30, 0, DateTimeKind.Utc);

    private sealed record G6TraditionalData(
        Guid StudentId, Guid ExerciseId, Guid TemplateId, Guid WorkoutId, Guid DayId,
        Guid WorkoutExerciseId, Guid HistoricWorkoutId, Guid HistoricDayId, Guid ExecutionId,
        Guid AssessmentId, Guid OldAssessmentId);

    private static G6TraditionalData SeedG6TraditionalRecords(TestApi factory, User user)
    {
        G6TraditionalData data = null!;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(x => x.UserId == user.Id);
            student.NoValidEnrollmentSince = new DateOnly(2024, 1, 1);
            var seedSuffix = Guid.NewGuid().ToString("N");
            var exercise = new Exercise { Id = Guid.NewGuid(), GymId = user.GymId, Name = $"G6 Existing Exercise {seedSuffix}", MuscleGroup = "Legs", IsActive = true };
            var template = new WorkoutTemplate { Id = Guid.NewGuid(), GymId = user.GymId, Name = $"G6 Existing Template {seedSuffix}", IsActive = true };
            var workout = new Workout { Id = Guid.NewGuid(), GymId = user.GymId, StudentId = student.Id, SourceWorkoutTemplateId = template.Id, Name = "G6 Existing Workout", IsActive = true };
            var day = new WorkoutDay { Id = Guid.NewGuid(), Name = "G6 Current Day", Order = 1 };
            var item = new WorkoutExercise { Id = Guid.NewGuid(), Exercise = exercise, Sets = 3, Repetitions = "10", RestSeconds = 60, Order = 1 };
            day.Exercises.Add(item);
            workout.Days.Add(day);
            var historic = new Workout { Id = Guid.NewGuid(), GymId = user.GymId, StudentId = student.Id, Name = "G6 Historic Workout", IsActive = false };
            var historicDay = new WorkoutDay { Id = Guid.NewGuid(), Name = "G6 Historic Day", Order = 1 };
            historicDay.Exercises.Add(new WorkoutExercise { Id = Guid.NewGuid(), Exercise = exercise, Sets = 2, Repetitions = "12", Order = 1 });
            historic.Days.Add(historicDay);
            var execution = new WorkoutExecution { Id = Guid.NewGuid(), WorkoutDay = historicDay, ExecutionDate = DateOnly.FromDateTime(G6HistoricTimestamp), CompletedAt = G6HistoricTimestamp };
            var assessment = new PhysicalAssessment { Id = Guid.NewGuid(), StudentId = student.Id, AssessmentDate = new DateOnly(2025, 3, 10), WeightKg = 75, HeightCm = 175, Notes = "Preserved synthetic assessment", CreatedAt = G6HistoricTimestamp, UpdatedAt = G6HistoricTimestamp };
            var oldAssessment = new PhysicalAssessment { Id = Guid.NewGuid(), StudentId = student.Id, AssessmentDate = new DateOnly(2025, 1, 10), WeightKg = 76, HeightCm = 175, Notes = "Preserved old synthetic assessment", CreatedAt = G6HistoricTimestamp, UpdatedAt = G6HistoricTimestamp };
            db.Exercises.Add(exercise);
            db.WorkoutTemplates.Add(template);
            db.Workouts.AddRange(workout, historic);
            db.WorkoutExecutions.Add(execution);
            db.PhysicalAssessments.AddRange(assessment, oldAssessment);
            data = new G6TraditionalData(student.Id, exercise.Id, template.Id, workout.Id, day.Id, item.Id,
                historic.Id, historicDay.Id, execution.Id, assessment.Id, oldAssessment.Id);
        });
        return data;
    }

    private static object[] G6TrainingDays(Guid exerciseId) =>
    [
        new
        {
            Name = "G6 Day", Order = 1,
            Exercises = new[] { new { ExerciseId = exerciseId, Sets = 3, Repetitions = "10", RestSeconds = 60, Order = 1 } }
        }
    ];

    private static async Task<JsonElement> ReadG6Json(HttpClient client, string endpoint)
    {
        using var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task ProcessG6Lifecycle(TestApi factory, Guid gymId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StudentLifecycleService>().ProcessAsync(gymId);
    }
}
