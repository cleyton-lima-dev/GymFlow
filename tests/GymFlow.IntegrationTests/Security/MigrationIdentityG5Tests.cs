using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task MigrationG5_StudentAliasesResolveCanonicalFinancialIdentity_Sqlite(bool persisted, bool byCharge) =>
        CheckG5CanonicalFinancialIdentity(false, persisted, byCharge);

    [PostgresTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task MigrationG5_StudentAliasesResolveCanonicalFinancialIdentity_PostgreSql(bool persisted, bool byCharge) =>
        CheckG5CanonicalFinancialIdentity(true, persisted, byCharge);

    private static async Task CheckG5CanonicalFinancialIdentity(bool postgres, bool persisted, bool byCharge)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var user = factory.AddUser(factory.EnabledGym);
        Guid studentId = Guid.Empty;
        factory.WithDb(db => studentId = db.Students.Single(student => student.UserId == user.Id).Id);
        using var client = G5Client(factory, admin);
        const string source = "g5-canonical-finance";
        var session = await CreateMigrationSession(client, "Aliases explícitos", source);
        await G5ConfirmAliases(client, session, studentId, "legacy_student_100", "client_2024_815");
        await G5ConfirmRows(client, session, 2,
            [new { rowNumber = 2, externalId = "plan-g5", name = "Plano canônico G5", price = "120.00", durationMonths = "12", billingCycle = "monthly" }]);
        await G5ConfirmRows(client, session, 3,
            [G5EnrollmentRow("enrollment-g5", "legacy_student_100", "plan-g5")]);
        if (byCharge)
            await G5ConfirmRows(client, session, 4,
                [new { rowNumber = 2, externalId = "finance-g5-base", studentExternalId = "legacy_student_100", enrollmentExternalId = "enrollment-g5", chargeState = "Pending" }]);
        if (persisted)
        {
            await G5Release(client, session);
            session = await CreateMigrationSession(client, "Cobrança com alias persistido", source);
        }

        var preview = await G5Preview(client, session, 4,
            [new { rowNumber = 2, externalId = "finance-g5", studentExternalId = "client_2024_815",
                enrollmentExternalId = byCharge ? null : "enrollment-g5", chargeExternalId = byCharge ? "finance-g5-base" : null, chargeState = "Pending" }]);
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean(), preview.ToString());
        var reviewStartedAt = DateTime.UtcNow;
        await G5ConfirmPreview(client, session, preview,
            [new { rowNumber = 2, action = "ImportNew", reviewedByUserId = Guid.NewGuid(), reviewedAt = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc) }]);
        var released = await G5Release(client, session);
        var financialResult = Assert.Single(released.GetProperty("results").EnumerateArray(), result =>
            result.GetProperty("externalId").GetString() == "finance-g5");
        Assert.Equal("ImportNew", financialResult.GetProperty("action").GetString());
        Assert.Equal(admin.Id, financialResult.GetProperty("reviewedByUserId").GetGuid());
        Assert.True(financialResult.GetProperty("reviewedAt").GetDateTime() >= reviewStartedAt);
        Assert.Equal(studentId, financialResult.GetProperty("effectiveStudentId").GetGuid());
        factory.WithDb(db =>
        {
            var student = Assert.Single(db.Students.Where(item => item.GymId == factory.EnabledGym));
            Assert.Equal(studentId, student.Id);
            Assert.Equal(user.Id, student.UserId);
            var enrollment = Assert.Single(db.Enrollments.Where(item => item.StudentId == studentId));
            var charge = Assert.Single(db.Charges.Where(item => item.EnrollmentId == enrollment.Id));
            Assert.Equal(ChargeStatus.Pending, charge.Status);
            Assert.Equal(ChargeOrigin.Migration, charge.Origin);
            Assert.Equal(PaymentOrigin.None, charge.PaymentOrigin);
            Assert.Null(charge.PaidAmount);
            Assert.Null(charge.PaidAt);
            var aliases = db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == source && reference.EntityType == MigrationEntityType.Students).ToList();
            Assert.Equal(2, aliases.Count);
            Assert.All(aliases, alias => Assert.Equal(studentId, alias.TargetEntityId));
            Assert.Equal(charge.Id, db.MigrationExternalReferences.Single(reference => reference.GymId == factory.EnabledGym &&
                reference.SourceNamespace == source && reference.ExternalId == "finance-g5").TargetEntityId);
            Assert.Empty(db.FinancialAuditLogs.Where(log => log.Action == FinancialAuditAction.PaymentConfirmed));
        });
    }

    [Fact]
    public Task MigrationG5_DifferentStudentAliasesCannotBypassFinancialDuplicateChecks_Sqlite() =>
        CheckG5AliasFinancialDuplicates(false);

    [PostgresFact]
    public Task MigrationG5_DifferentStudentAliasesCannotBypassFinancialDuplicateChecks_PostgreSql() =>
        CheckG5AliasFinancialDuplicates(true);

    private static async Task CheckG5AliasFinancialDuplicates(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = G5Client(factory, admin);
        const string source = "g5-financial-duplicate";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-duplicate", "g5-duplicate");
        var session = await CreateMigrationSession(client, "Alias adicional", source);
        await G5ConfirmAliases(client, session, target.StudentId, "student-alias-b");
        await G5Release(client, session);
        session = await CreateMigrationSession(client, "Duplicidade por identidade", source);
        var preview = await G5Preview(client, session, 4,
        [
            new { rowNumber = 2, externalId = "financial-a", studentExternalId = target.StudentExternalId, enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" },
            new { rowNumber = 3, externalId = "financial-b", studentExternalId = "student-alias-b", enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" }
        ]);
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row =>
        {
            Assert.False(row.GetProperty("isValid").GetBoolean());
            Assert.Contains("mesma matrícula/cobrança", G5Errors(row), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("não corresponde", G5Errors(row), StringComparison.OrdinalIgnoreCase);
        });
        var rejected = await G5ConfirmResponse(client, session, preview,
            [new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await G5ConfirmPreview(client, session, preview,
            [new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "Ignore" }]);
        await G5Release(client, session);
        factory.WithDb(db =>
        {
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
            Assert.Single(db.Enrollments.Where(enrollment => enrollment.StudentId == target.StudentId));
            Assert.Single(db.Charges.Where(charge => charge.EnrollmentId == target.EnrollmentId));
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session &&
                reference.EntityType == MigrationEntityType.Charges));
        });
    }

    [Fact]
    public Task MigrationG5_IndependentOriginsKeepIdenticalExternalIdsSeparate_Sqlite() => G5IndependentOrigins(false);

    [PostgresFact]
    public Task MigrationG5_IndependentOriginsKeepIdenticalExternalIdsSeparate_PostgreSql() => G5IndependentOrigins(true);

    private static async Task G5IndependentOrigins(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var first = G5AddAdministrativeStudent(factory, "Pessoa A");
        var second = G5AddAdministrativeStudent(factory, "Pessoa B");
        using var client = G5Client(factory, admin);
        var a = await CreateMigrationSession(client, "Origem A", "g5-origin-a");
        await G5ConfirmAliases(client, a, first.Id, "same-id");
        await G5Release(client, a);
        var b = await CreateMigrationSession(client, "Origem B", "g5-origin-b");
        await G5ConfirmAliases(client, b, second.Id, "same-id");
        await G5ConfirmAliases(client, b, first.Id, "another-id-for-a");
        await G5Release(client, b);
        factory.WithDb(db =>
        {
            var references = db.MigrationExternalReferences.Where(reference => reference.GymId == factory.EnabledGym &&
                reference.EntityType == MigrationEntityType.Students).ToList();
            Assert.Equal(3, references.Count);
            Assert.Equal(first.Id, references.Single(reference => reference.SourceNamespace == "g5-origin-a").TargetEntityId);
            Assert.Equal(second.Id, references.Single(reference => reference.SourceNamespace == "g5-origin-b" &&
                reference.ExternalId == "same-id").TargetEntityId);
            Assert.Equal(first.Id, references.Single(reference => reference.SourceNamespace == "g5-origin-b" &&
                reference.ExternalId == "another-id-for-a").TargetEntityId);
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
        });
    }

    [Fact]
    public Task MigrationG5_OpaqueExternalIdsKeepCaseAndTrimOnlyOuterWhitespace_Sqlite() => G5OpaqueIds(false);

    [PostgresFact]
    public Task MigrationG5_OpaqueExternalIdsKeepCaseAndTrimOnlyOuterWhitespace_PostgreSql() => G5OpaqueIds(true);

    private static async Task G5OpaqueIds(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var first = G5AddAdministrativeStudent(factory, "Pessoa A");
        var second = G5AddAdministrativeStudent(factory, "Pessoa B");
        using var client = G5Client(factory, admin);
        var session = await CreateMigrationSession(client, "IDs opacos", "g5-case-sensitive");
        var preview = await G5Preview(client, session, 1,
        [
            new { rowNumber = 2, externalId = "  Student_A  ", name = "Linha A" },
            new { rowNumber = 3, externalId = "student_a", name = "Linha B" }
        ]);
        Assert.All(preview.GetProperty("rows").EnumerateArray(), row => Assert.True(row.GetProperty("isValid").GetBoolean(), row.ToString()));
        await G5ConfirmPreview(client, session, preview,
        [
            new { rowNumber = 2, action = "LinkExisting", targetId = first.Id },
            new { rowNumber = 3, action = "LinkExisting", targetId = second.Id }
        ]);
        await G5Release(client, session);
        factory.WithDb(db =>
        {
            var references = db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session).ToList();
            Assert.Equal(2, references.Count);
            Assert.Equal(first.Id, references.Single(reference => reference.ExternalId == "Student_A").TargetEntityId);
            Assert.Equal(second.Id, references.Single(reference => reference.ExternalId == "student_a").TargetEntityId);
        });
    }

    [Fact]
    public Task MigrationG5_KnownAliasCannotBeReassignedUsingContradictoryContactEvidence_Sqlite() => G5AliasConflict(false);

    [PostgresFact]
    public Task MigrationG5_KnownAliasCannotBeReassignedUsingContradictoryContactEvidence_PostgreSql() => G5AliasConflict(true);

    private static async Task G5AliasConflict(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var first = G5AddAdministrativeStudent(factory, "Pessoa canônica");
        var second = G5AddAdministrativeStudent(factory, "Pessoa conflitante", "11987654321");
        using var client = G5Client(factory, admin);
        const string source = "g5-immutable-alias";
        var initial = await CreateMigrationSession(client, "Vínculo original", source);
        await G5ConfirmAliases(client, initial, first.Id, "known-id");
        await G5Release(client, initial);
        var session = await CreateMigrationSession(client, "Tentativa de troca", source);
        var preview = await G5Preview(client, session, 1,
            [new { rowNumber = 2, externalId = "known-id", name = "Outra pessoa do arquivo", phone = second.Phone }]);
        var response = await G5ConfirmResponse(client, session, preview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = second.Id }]);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationBatchStatus.Previewed, db.MigrationBatches.Single(batch =>
                batch.Id == preview.GetProperty("batchId").GetGuid()).Status);
            Assert.Equal(first.Id, db.MigrationExternalReferences.Single(reference => reference.SourceNamespace == source &&
                reference.ExternalId == "known-id").TargetEntityId);
        });
        var ambiguousImport = await G5ConfirmResponse(client, session, preview, [new { rowNumber = 2, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.BadRequest, ambiguousImport.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(first.Id, db.MigrationExternalReferences.Single(reference => reference.SourceNamespace == source &&
                reference.ExternalId == "known-id").TargetEntityId);
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
        });
        await G5ConfirmPreview(client, session, preview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = first.Id }]);
        await G5Release(client, session);
        factory.WithDb(db =>
        {
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Equal("Pessoa canônica", db.Students.Single(student => student.Id == first.Id).Name);
            Assert.Equal(first.Id, db.MigrationExternalReferences.Single(reference => reference.SourceNamespace == source &&
                reference.ExternalId == "known-id").TargetEntityId);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_ExplicitStudentTargetMustExistInCurrentGym_Sqlite(bool foreign) => G5InvalidTarget(false, foreign);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_ExplicitStudentTargetMustExistInCurrentGym_PostgreSql(bool foreign) => G5InvalidTarget(true, foreign);

    private static async Task G5InvalidTarget(bool postgres, bool foreign)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignStudent = G5AddAdministrativeStudent(factory, "Pessoa de outra academia", gymId: factory.DisabledGym);
        using var client = G5Client(factory, admin);
        var session = await CreateMigrationSession(client, "Tentativa inválida", "g5-invalid-target");
        var preview = await G5Preview(client, session, 1,
            [new { rowNumber = 2, externalId = "external-id", name = foreignStudent.Name }]);
        var rejected = await G5ConfirmResponse(client, session, preview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = foreign ? foreignStudent.Id : Guid.NewGuid() }]);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("academia", await rejected.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Empty(db.MigrationExternalReferences);
            Assert.Empty(db.Students.Where(student => student.GymId == factory.EnabledGym));
            Assert.Equal(foreignStudent.Id, Assert.Single(db.Students).Id);
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
        });
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    public Task MigrationG5_LinkPreservesStudentDigitalAndOperationalState_Sqlite(bool digital, bool active, bool archived) =>
        G5PreserveStudentState(false, digital, active, archived);

    [PostgresTheory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    public Task MigrationG5_LinkPreservesStudentDigitalAndOperationalState_PostgreSql(bool digital, bool active, bool archived) =>
        G5PreserveStudentState(true, digital, active, archived);

    [Fact]
    public Task MigrationG5_LinkCannotReactivateInactiveDigitalAccountOfActiveStudent_Sqlite() =>
        G5PreserveStudentState(false, digital: true, active: true, archived: false, userActive: false);

    [PostgresFact]
    public Task MigrationG5_LinkCannotReactivateInactiveDigitalAccountOfActiveStudent_PostgreSql() =>
        G5PreserveStudentState(true, digital: true, active: true, archived: false, userActive: false);

    private static async Task G5PreserveStudentState(bool postgres, bool digital, bool active, bool archived, bool? userActive = null)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var user = digital ? factory.AddUser(factory.EnabledGym, active: userActive ?? active) : null;
        Student student = null!;
        factory.WithDb(db =>
        {
            if (user is not null) student = db.Students.Single(item => item.UserId == user.Id);
            else
            {
                student = new Student { Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Pessoa sem e-mail", IsActive = active };
                db.Students.Add(student);
            }
        });
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var workoutId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var checkInId = Guid.NewGuid();
        var paidEnrollmentId = Guid.NewGuid();
        var cancelledEnrollmentId = Guid.NewGuid();
        var paidChargeId = Guid.NewGuid();
        var cancelledChargeId = Guid.NewGuid();
        var paymentAuditId = Guid.NewGuid();
        factory.WithDb(db =>
        {
            var existing = db.Students.Single(item => item.Id == student.Id);
            existing.IsActive = active;
            existing.ArchivedAt = archived ? timestamp : null;
            existing.InactivatedAt = active ? null : timestamp;
            existing.InactivationReason = active ? null : StudentInactivationReason.Manual;
            existing.NoValidEnrollmentSince = new DateOnly(2025, 1, 1);
            existing.Phone = "11912345678";
            db.Workouts.Add(new Workout { Id = workoutId, StudentId = student.Id, GymId = factory.EnabledGym, Name = "Treino preservado" });
            db.PhysicalAssessments.Add(new PhysicalAssessment
            {
                Id = assessmentId, StudentId = student.Id, AssessmentDate = new DateOnly(2026, 1, 2), WeightKg = 75m, HeightCm = 175m
            });
            db.CheckIns.Add(new CheckIn
            {
                Id = checkInId, StudentId = student.Id, GymId = factory.EnabledGym, RequestId = Guid.NewGuid(),
                Source = CheckInSource.Manual, OccurredAt = timestamp, RecordedByUserId = admin.Id
            });
            var plan = new Plan { Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Plano operacional preservado", Price = 100m, DurationMonths = 1 };
            db.Plans.Add(plan);
            db.Enrollments.AddRange(
                new Enrollment
                {
                    Id = paidEnrollmentId, StudentId = student.Id, PlanId = plan.Id, PlanName = plan.Name, PlanPrice = plan.Price,
                    PlanDurationMonths = 1, StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 2, 1), Status = EnrollmentStatus.Expired,
                    Charge = new Charge
                    {
                        Id = paidChargeId, EnrollmentId = paidEnrollmentId, Amount = 100m, PaidAmount = 95m, DiscountAmount = 5m,
                        PaidAt = timestamp, PaymentMethod = PaymentMethod.Pix, DueDate = new DateOnly(2025, 1, 1), Status = ChargeStatus.Paid,
                        Origin = ChargeOrigin.Avelri, PaymentOrigin = PaymentOrigin.Avelri
                    }
                },
                new Enrollment
                {
                    Id = cancelledEnrollmentId, StudentId = student.Id, PlanId = plan.Id, PlanName = plan.Name, PlanPrice = plan.Price,
                    PlanDurationMonths = 1, StartDate = new DateOnly(2025, 2, 1), EndDate = new DateOnly(2025, 3, 1),
                    Status = EnrollmentStatus.Cancelled, CancellationDate = new DateOnly(2025, 2, 2),
                    Charge = new Charge
                    {
                        Id = cancelledChargeId, EnrollmentId = cancelledEnrollmentId, Amount = 100m,
                        DueDate = new DateOnly(2025, 2, 1), Status = ChargeStatus.Cancelled
                    }
                });
            db.FinancialAuditLogs.Add(new FinancialAuditLog
            {
                Id = paymentAuditId, GymId = factory.EnabledGym, ActorUserId = admin.Id, EntityType = "Charge", EntityId = paidChargeId,
                Action = FinancialAuditAction.PaymentConfirmed, PreviousValues = "{}", NewValues = "{\"paidAmount\":95}", OccurredAt = timestamp
            });
        });
        using var client = G5Client(factory, admin);
        var session = await CreateMigrationSession(client, "Preservação de estado", "g5-preservation");
        await G5ConfirmAliases(client, session, student.Id, "historical-id", "new-source-id");
        var released = await G5Release(client, session);
        Assert.All(released.GetProperty("results").EnumerateArray(), result =>
        {
            Assert.Equal(student.Id, result.GetProperty("targetId").GetGuid());
            Assert.Equal("LinkExisting", result.GetProperty("action").GetString());
            Assert.Equal(admin.Id, result.GetProperty("reviewedByUserId").GetGuid());
        });
        factory.WithDb(db =>
        {
            var existing = Assert.Single(db.Students.Where(item => item.GymId == factory.EnabledGym));
            Assert.Equal(student.Id, existing.Id);
            Assert.Equal(user?.Id, existing.UserId);
            Assert.Equal(active, existing.IsActive);
            Assert.Equal(digital, existing.AppAccessEnabled);
            Assert.Equal(archived ? timestamp : (DateTime?)null, existing.ArchivedAt);
            Assert.Equal(active ? null : (DateTime?)timestamp, existing.InactivatedAt);
            Assert.Equal(active ? null : (StudentInactivationReason?)StudentInactivationReason.Manual, existing.InactivationReason);
            Assert.Equal(new DateOnly(2025, 1, 1), existing.NoValidEnrollmentSince);
            Assert.Equal("11912345678", existing.Phone);
            Assert.Equal(user?.Email, existing.Email);
            Assert.Equal(user?.Name ?? "Pessoa sem e-mail", existing.Name);
            Assert.Equal(digital ? 2 : 1, db.Users.Count());
            if (user is not null)
            {
                var persistedUser = db.Users.Single(item => item.Id == user.Id);
                Assert.Equal(userActive ?? active, persistedUser.IsActive);
                Assert.Equal(user.PasswordHash, persistedUser.PasswordHash);
                Assert.Equal(user.Email, persistedUser.Email);
            }
            Assert.Equal(student.Id, db.Workouts.Single(item => item.Id == workoutId).StudentId);
            Assert.Equal("Treino preservado", db.Workouts.Single(item => item.Id == workoutId).Name);
            Assert.Equal(student.Id, db.PhysicalAssessments.Single(item => item.Id == assessmentId).StudentId);
            Assert.Equal(75m, db.PhysicalAssessments.Single(item => item.Id == assessmentId).WeightKg);
            Assert.Equal(student.Id, db.CheckIns.Single(item => item.Id == checkInId).StudentId);
            Assert.Equal(timestamp, db.CheckIns.Single(item => item.Id == checkInId).OccurredAt);
            var paid = db.Charges.Single(item => item.Id == paidChargeId);
            Assert.Equal(ChargeStatus.Paid, paid.Status);
            Assert.Equal(95m, paid.PaidAmount);
            Assert.Equal(5m, paid.DiscountAmount);
            Assert.Equal(timestamp, paid.PaidAt);
            Assert.Equal(PaymentMethod.Pix, paid.PaymentMethod);
            Assert.Equal(PaymentOrigin.Avelri, paid.PaymentOrigin);
            Assert.Equal(EnrollmentStatus.Cancelled, db.Enrollments.Single(item => item.Id == cancelledEnrollmentId).Status);
            Assert.Equal(new DateOnly(2025, 2, 2), db.Enrollments.Single(item => item.Id == cancelledEnrollmentId).CancellationDate);
            Assert.Equal(ChargeStatus.Cancelled, db.Charges.Single(item => item.Id == cancelledChargeId).Status);
            Assert.Equal(paymentAuditId, Assert.Single(db.FinancialAuditLogs).Id);
            Assert.Equal(2, db.Enrollments.Count());
            Assert.Equal(2, db.Charges.Count());
        });
    }

    [Fact]
    public Task MigrationG5_ReimportIsIdempotentAcrossSessions_Sqlite() => G5Reimport(false);

    [PostgresFact]
    public Task MigrationG5_ReimportIsIdempotentAcrossSessions_PostgreSql() => G5Reimport(true);

    private static async Task G5Reimport(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = G5Client(factory, admin);
        const string source = "g5-reimport";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "enrollment-reimport", "g5-reimport");
        Guid planId = Guid.Empty;
        Guid chargeId = Guid.Empty;
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Single(item => item.Id == target.EnrollmentId);
            planId = enrollment.PlanId;
            chargeId = db.Charges.Single(item => item.EnrollmentId == enrollment.Id).Id;
        });
        var session = await CreateMigrationSession(client, "Mesmo arquivo novamente", source);
        await G5ConfirmRows(client, session, 1,
            [new { rowNumber = 2, externalId = target.StudentExternalId, name = "Nome não deve sobrescrever" }]);
        var plan = await G5Preview(client, session, 2,
            [new { rowNumber = 2, externalId = "plan-g5-reimport", name = "Plano g5-reimport sintético", price = "120.00", durationMonths = "12", billingCycle = "monthly" }]);
        await G5ConfirmPreview(client, session, plan, [new { rowNumber = 2, action = "LinkExisting", targetId = planId }]);
        await G5ConfirmRows(client, session, 3,
            [G5EnrollmentRow(target.EnrollmentExternalId, target.StudentExternalId, "plan-g5-reimport")]);
        await G5ConfirmRows(client, session, 4,
            [new { rowNumber = 2, externalId = "charge-g5-reimport-base", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" }]);
        var firstRelease = await G5Release(client, session);
        var retryRelease = await G5Release(client, session);
        Assert.Equal(firstRelease.GetProperty("results").ToString(), retryRelease.GetProperty("results").ToString());
        factory.WithDb(db =>
        {
            Assert.Equal(target.StudentId, Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym)).Id);
            Assert.Equal("Aluno g5-reimport sintético", db.Students.Single(student => student.Id == target.StudentId).Name);
            Assert.Equal(target.EnrollmentId, Assert.Single(db.Enrollments).Id);
            Assert.Equal(chargeId, Assert.Single(db.Charges).Id);
            Assert.Equal(planId, Assert.Single(db.Plans).Id);
            Assert.Equal(4, db.MigrationExternalReferences.Count(reference => reference.SourceNamespace == source));
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
            Assert.Empty(db.FinancialAuditLogs);
        });
    }

    [Fact]
    public Task MigrationG5_UnknownStudentAndWrongCanonicalStudentAreRejected_Sqlite() => G5MissingAndWrongStudent(false);

    [PostgresFact]
    public Task MigrationG5_UnknownStudentAndWrongCanonicalStudentAreRejected_PostgreSql() => G5MissingAndWrongStudent(true);

    private static async Task G5MissingAndWrongStudent(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = G5Client(factory, admin);
        var session = await CreateMigrationSession(client, "Sem fusão por nome", "g5-distinct-students");
        await G5ConfirmRows(client, session, 1,
        [
            new { rowNumber = 2, externalId = "person-a", name = "Mesmo nome" },
            new { rowNumber = 3, externalId = "person-b", name = "Mesmo nome" }
        ]);
        await G5ConfirmRows(client, session, 2,
            [new { rowNumber = 2, externalId = "plan-distinct", name = "Plano pessoas distintas", price = "80.00", durationMonths = "12", billingCycle = "monthly" }]);
        await G5ConfirmRows(client, session, 3,
        [
            G5EnrollmentRow("enrollment-a", "person-a", "plan-distinct"),
            G5EnrollmentRow("enrollment-b", "person-b", "plan-distinct", rowNumber: 3)
        ]);
        var wrong = await G5Preview(client, session, 4,
        [
            new { rowNumber = 2, externalId = "wrong-person", studentExternalId = "person-a", enrollmentExternalId = "enrollment-b", chargeState = "Pending" },
            new { rowNumber = 3, externalId = "unknown-person", studentExternalId = "person-missing", enrollmentExternalId = "enrollment-a", chargeState = "Pending" }
        ]);
        Assert.All(wrong.GetProperty("rows").EnumerateArray(), row => Assert.False(row.GetProperty("isValid").GetBoolean()));
        Assert.Contains("não corresponde", G5Errors(wrong.GetProperty("rows")[0]), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Aluno externo não localizado", G5Errors(wrong.GetProperty("rows")[1]), StringComparison.OrdinalIgnoreCase);
        var rejected = await G5ConfirmResponse(client, session, wrong,
            [new { rowNumber = 2, action = "ImportNew" }, new { rowNumber = 3, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await G5ConfirmPreview(client, session, wrong,
            [new { rowNumber = 2, action = "Ignore" }, new { rowNumber = 3, action = "Ignore" }]);
        await G5Release(client, session);
        factory.WithDb(db =>
        {
            var students = db.Students.Where(student => student.GymId == factory.EnabledGym).ToList();
            Assert.Equal(2, students.Count);
            Assert.All(students, student => Assert.Equal("Mesmo nome", student.Name));
            var enrollments = db.Enrollments.ToList();
            Assert.Equal(2, enrollments.Count);
            Assert.Equal(2, enrollments.Select(enrollment => enrollment.StudentId).Distinct().Count());
            Assert.Equal(2, db.Charges.Count());
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.EntityType == MigrationEntityType.Charges));
        });
    }

    [Fact]
    public Task MigrationG5_ConfirmedFinancialReviewCanRecoverAfterStateChange_Sqlite() => G5RecoverFinancialReview(false);

    [PostgresFact]
    public Task MigrationG5_ConfirmedFinancialReviewCanRecoverAfterStateChange_PostgreSql() => G5RecoverFinancialReview(true);

    private static async Task G5RecoverFinancialReview(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = G5Client(factory, admin);
        const string source = "g5-recover-financial";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "recover-enrollment", "g5-recover");
        var session = await CreateMigrationSession(client, "Revisão financeira recuperável", source);
        var preview = await G5Preview(client, session, 4,
            [new { rowNumber = 2, externalId = "recover-finance", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" }]);
        await G5ConfirmPreview(client, session, preview, [new { rowNumber = 2, action = "ImportNew" }]);
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId);
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.Charge!.Status = ChargeStatus.Paid;
            enrollment.Charge.PaymentOrigin = PaymentOrigin.Migration;
        });
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == session).Status);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
        });
        var reviewResponse = await client.GetAsync($"/api/migrations/{session}/batches/{preview.GetProperty("batchId").GetGuid()}/review");
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = await reviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(preview.GetProperty("fingerprint").GetString(), review.GetProperty("fingerprint").GetString());
        Assert.True(review.GetProperty("rows")[0].GetProperty("requiresFinancialResolution").GetBoolean());
        Assert.Empty(review.GetProperty("decisions").EnumerateArray());
        await G5ConfirmPreview(client, session, review,
            [new { rowNumber = 2, action = "ImportNew", financialStateResolution = "UseEnrollmentState" }]);
        var release = await G5Release(client, session);
        Assert.Equal("UseEnrollmentState", Assert.Single(release.GetProperty("results").EnumerateArray())
            .GetProperty("financialStateResolution").GetString());
        factory.WithDb(db =>
        {
            var enrollment = Assert.Single(db.Enrollments);
            Assert.Equal(target.EnrollmentId, enrollment.Id);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            var charge = Assert.Single(db.Charges);
            Assert.Equal(ChargeStatus.Paid, charge.Status);
            Assert.Equal(PaymentOrigin.Migration, charge.PaymentOrigin);
            Assert.Null(charge.PaidAmount);
            Assert.Null(charge.PaidAt);
            Assert.Single(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
            Assert.Empty(db.FinancialAuditLogs);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_KnownFinancialAliasReimportPreservesOperationalPaymentOrCancellation_Sqlite(bool cancelled) =>
        G5ReimportAfterOperations(false, cancelled);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_KnownFinancialAliasReimportPreservesOperationalPaymentOrCancellation_PostgreSql(bool cancelled) =>
        G5ReimportAfterOperations(true, cancelled);

    private static async Task G5ReimportAfterOperations(bool postgres, bool cancelled)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = G5Client(factory, admin);
        const string source = "g5-reimport-operational";
        var target = await CreateReleasedMigratedEnrollment(factory, client, source, "operational-enrollment", "g5-operational");
        Guid chargeId = Guid.Empty;
        factory.WithDb(db => chargeId = db.Charges.Single(item => item.EnrollmentId == target.EnrollmentId).Id);
        var operation = cancelled
            ? await client.PatchAsync($"/api/enrollments/{target.EnrollmentId}/cancel", null)
            : await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", new
            {
                paidAmount = 120m, discountAmount = 0m, paymentMethod = (int)PaymentMethod.Pix, paidAt = DateTimeOffset.UtcNow
            });
        Assert.True(operation.StatusCode == HttpStatusCode.NoContent,
            $"{operation.StatusCode}: {await operation.Content.ReadAsStringAsync()}");
        string before = "";
        factory.WithDb(db => before = G5FinancialSnapshot(db.Enrollments.Include(item => item.Charge).Single(item => item.Id == target.EnrollmentId)));
        var session = await CreateMigrationSession(client, "Reimportação após operação real", source);
        await G5ConfirmRows(client, session, 1,
            [new { rowNumber = 2, externalId = target.StudentExternalId, name = "Arquivo antigo não altera aluno" }]);
        await G5ConfirmRows(client, session, 3,
            [G5EnrollmentRow(target.EnrollmentExternalId, target.StudentExternalId, "plan-g5-operational")]);
        var preview = await G5Preview(client, session, 4,
            [new { rowNumber = 2, externalId = "charge-g5-operational-base", studentExternalId = target.StudentExternalId,
                enrollmentExternalId = target.EnrollmentExternalId, chargeState = "Pending" }]);
        Assert.True(preview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean(), preview.ToString());
        await G5ConfirmPreview(client, session, preview, [new { rowNumber = 2, action = "ImportNew" }]);
        var release = await G5Release(client, session);
        Assert.All(release.GetProperty("results").EnumerateArray(), result => Assert.Equal("AlreadyLinked", result.GetProperty("status").GetString()));
        factory.WithDb(db =>
        {
            var enrollment = Assert.Single(db.Enrollments.Include(item => item.Charge));
            Assert.Equal(before, G5FinancialSnapshot(enrollment));
            Assert.Equal(cancelled ? EnrollmentStatus.Cancelled : EnrollmentStatus.Active, enrollment.Status);
            var charge = Assert.Single(db.Charges);
            Assert.Equal(cancelled ? ChargeStatus.Cancelled : ChargeStatus.Paid, charge.Status);
            if (!cancelled)
            {
                Assert.Equal(PaymentOrigin.Avelri, charge.PaymentOrigin);
                Assert.Equal(120m, charge.PaidAmount);
                Assert.Equal(PaymentMethod.Pix, charge.PaymentMethod);
                Assert.NotNull(charge.PaidAt);
            }
            var audit = Assert.Single(db.FinancialAuditLogs);
            Assert.Equal(cancelled ? FinancialAuditAction.ChargeCancelled : FinancialAuditAction.PaymentConfirmed, audit.Action);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
            Assert.Equal(4, db.MigrationExternalReferences.Count(reference => reference.SourceNamespace == source));
            Assert.Single(db.Students.Where(student => student.GymId == factory.EnabledGym));
        });
    }

    private static string G5FinancialSnapshot(Enrollment enrollment) => JsonSerializer.Serialize(new
    {
        enrollment.Id, enrollment.StudentId, enrollment.PlanId, EnrollmentStatus = enrollment.Status, enrollment.StartDate,
        enrollment.EndDate, enrollment.CancellationDate, EnrollmentUpdatedAt = enrollment.UpdatedAt,
        ChargeId = enrollment.Charge!.Id, enrollment.Charge.Amount, enrollment.Charge.DiscountAmount, enrollment.Charge.PaidAmount,
        enrollment.Charge.PaymentMethod, enrollment.Charge.PaidAt, ChargeStatus = enrollment.Charge.Status,
        enrollment.Charge.PaymentOrigin, enrollment.Charge.Origin, enrollment.Charge.VerificationStatus, ChargeUpdatedAt = enrollment.Charge.UpdatedAt
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_StudentDecisionChangeRequiresEnrollmentReviewAgain_Sqlite(bool enrollmentConfirmed) =>
        G5StudentDecisionChangeRequiresEnrollmentReview(false, enrollmentConfirmed);

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MigrationG5_StudentDecisionChangeRequiresEnrollmentReviewAgain_PostgreSql(bool enrollmentConfirmed) =>
        G5StudentDecisionChangeRequiresEnrollmentReview(true, enrollmentConfirmed);

    private static async Task G5StudentDecisionChangeRequiresEnrollmentReview(bool postgres, bool enrollmentConfirmed)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var first = G5AddAdministrativeStudent(factory, "Aluno inicialmente escolhido");
        var second = G5AddAdministrativeStudent(factory, "Aluno escolhido após revisão");
        using var client = G5Client(factory, admin);
        const string source = "g5-enrollment-student-revision";
        var session = await CreateMigrationSession(client, "Dependência de identidade revisada", source);
        var studentPreview = await G5Preview(client, session, 1,
            [new { rowNumber = 2, externalId = "new-student-alias", name = "Nome sem evidência automática" }]);
        await G5ConfirmPreview(client, session, studentPreview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = first.Id }]);
        await G5ConfirmRows(client, session, 2,
            [new { rowNumber = 2, externalId = "dependency-plan", name = "Plano da identidade revisada", price = "100.00", durationMonths = "12", billingCycle = "monthly" }]);
        var enrollmentPreview = await G5Preview(client, session, 3,
            [G5EnrollmentRow("dependency-enrollment", "new-student-alias", "dependency-plan")]);
        Assert.True(enrollmentPreview.GetProperty("rows")[0].GetProperty("isValid").GetBoolean(), enrollmentPreview.ToString());
        if (enrollmentConfirmed)
            await G5ConfirmPreview(client, session, enrollmentPreview, [new { rowNumber = 2, action = "ImportNew" }]);

        var studentReviewResponse = await client.GetAsync($"/api/migrations/{session}/batches/{studentPreview.GetProperty("batchId").GetGuid()}/review");
        Assert.Equal(HttpStatusCode.OK, studentReviewResponse.StatusCode);
        var studentReview = await studentReviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        await G5ConfirmPreview(client, session, studentReview,
            [new { rowNumber = 2, action = "LinkExisting", targetId = second.Id }]);
        var rejected = enrollmentConfirmed
            ? await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { })
            : await G5ConfirmResponse(client, session, enrollmentPreview, [new { rowNumber = 2, action = "ImportNew" }]);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(MigrationSessionStatus.Preparing, db.MigrationSessions.Single(item => item.Id == session).Status);
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Empty(db.Plans.Where(plan => plan.GymId == factory.EnabledGym));
            Assert.Empty(db.Enrollments);
            Assert.Empty(db.Charges);
            Assert.Empty(db.MigrationExternalReferences.Where(reference => reference.MigrationSessionId == session));
        });
        var enrollmentReviewResponse = await client.GetAsync($"/api/migrations/{session}/batches/{enrollmentPreview.GetProperty("batchId").GetGuid()}/review");
        Assert.Equal(HttpStatusCode.OK, enrollmentReviewResponse.StatusCode);
        var enrollmentReview = await enrollmentReviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(enrollmentPreview.GetProperty("fingerprint").GetString(), enrollmentReview.GetProperty("fingerprint").GetString());
        Assert.Equal($"student:{second.Id:D}", enrollmentReview.GetProperty("rows")[0].GetProperty("values").GetProperty("resolvedStudentKey").GetString());
        Assert.Empty(enrollmentReview.GetProperty("decisions").EnumerateArray());
        await G5ConfirmPreview(client, session, enrollmentReview, [new { rowNumber = 2, action = "ImportNew" }]);
        var release = await G5Release(client, session);
        var enrollmentResult = Assert.Single(release.GetProperty("results").EnumerateArray(), result =>
            result.GetProperty("externalId").GetString() == "dependency-enrollment");
        Assert.Equal(second.Id, enrollmentResult.GetProperty("effectiveStudentId").GetGuid());
        factory.WithDb(db =>
        {
            var enrollment = Assert.Single(db.Enrollments.Include(item => item.Charge));
            Assert.Equal(second.Id, enrollment.StudentId);
            Assert.Equal(enrollment.Id, Assert.Single(db.Charges).EnrollmentId);
            Assert.Equal(second.Id, db.MigrationExternalReferences.Single(reference =>
                reference.MigrationSessionId == session && reference.EntityType == MigrationEntityType.Students).TargetEntityId);
            Assert.Equal(3, db.MigrationExternalReferences.Count(reference => reference.MigrationSessionId == session));
            Assert.Empty(db.Enrollments.Where(item => item.StudentId == first.Id));
            Assert.Equal(2, db.Students.Count(student => student.GymId == factory.EnabledGym));
            Assert.Empty(db.FinancialAuditLogs);
        });
    }

    private static HttpClient G5Client(TestApi factory, User admin)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        return client;
    }

    private static Student G5AddAdministrativeStudent(TestApi factory, string name, string? phone = null,
        bool active = true, Guid? gymId = null)
    {
        var student = new Student { Id = Guid.NewGuid(), GymId = gymId ?? factory.EnabledGym, Name = name, Phone = phone, IsActive = active };
        factory.WithDb(db => db.Students.Add(student));
        return student;
    }

    private static object G5EnrollmentRow(string externalId, string studentExternalId, string planExternalId, int rowNumber = 2)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new { rowNumber, externalId, studentExternalId, planExternalId,
            startDate = today.ToString("yyyy-MM-dd"), endDate = today.AddMonths(12).ToString("yyyy-MM-dd"), chargeState = "Pending" };
    }

    private static async Task G5ConfirmAliases(HttpClient client, Guid session, Guid studentId, params string[] aliases)
    {
        var rows = aliases.Select((alias, index) => (object)new { rowNumber = index + 2, externalId = alias, name = $"Nome do arquivo {index}" }).ToArray();
        var preview = await G5Preview(client, session, 1, rows);
        var decisions = aliases.Select((_, index) => (object)new { rowNumber = index + 2, action = "LinkExisting", targetId = studentId }).ToArray();
        await G5ConfirmPreview(client, session, preview, decisions);
    }

    private static async Task<JsonElement> G5Preview(HttpClient client, Guid session, int type, object[] rows)
    {
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/preview", new { entityType = type, rows });
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task G5ConfirmRows(HttpClient client, Guid session, int type, object[] rows)
    {
        var preview = await G5Preview(client, session, type, rows);
        var decisions = preview.GetProperty("rows").EnumerateArray().Select(row =>
            (object)new { rowNumber = row.GetProperty("rowNumber").GetInt32(), action = "ImportNew" }).ToArray();
        await G5ConfirmPreview(client, session, preview, decisions);
    }

    private static async Task<HttpResponseMessage> G5ConfirmResponse(HttpClient client, Guid session, JsonElement preview, object[] decisions) =>
        await client.PostAsJsonAsync($"/api/migrations/{session}/batches/{preview.GetProperty("batchId").GetGuid()}/confirm",
            new { fingerprint = preview.GetProperty("fingerprint").GetString(), decisions });

    private static async Task G5ConfirmPreview(HttpClient client, Guid session, JsonElement preview, object[] decisions)
    {
        var response = await G5ConfirmResponse(client, session, preview, decisions);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonElement> G5Release(HttpClient client, Guid session)
    {
        var response = await client.PostAsJsonAsync($"/api/migrations/{session}/release", new { });
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string G5Errors(JsonElement row) => string.Join(" ", row.GetProperty("errors").EnumerateArray().Select(error => error.GetString()));
}
