CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810213559_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810213559_InitialCreate', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810231219_AddUsers') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "GymId" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Email" character varying(200) NOT NULL,
        "PasswordHash" text NOT NULL,
        "Role" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810231219_AddUsers') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810231219_AddUsers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810231219_AddUsers', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811220007_AddStudents') THEN
    CREATE TABLE "Students" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "BirthDate" date,
        "Phone" character varying(20),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Students" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Students_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811220007_AddStudents') THEN
    CREATE UNIQUE INDEX "IX_Students_UserId" ON "Students" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811220007_AddStudents') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260811220007_AddStudents', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812211536_AddExercises') THEN
    CREATE TABLE "Exercises" (
        "Id" uuid NOT NULL,
        "GymId" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "MuscleGroup" character varying(100) NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Exercises" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812211536_AddExercises') THEN
    CREATE INDEX "IX_Exercises_GymId" ON "Exercises" ("GymId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812211536_AddExercises') THEN
    CREATE UNIQUE INDEX "IX_Exercises_GymId_Name" ON "Exercises" ("GymId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812211536_AddExercises') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812211536_AddExercises', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812225133_MakeExerciseNameCaseInsensitive') THEN
    CREATE EXTENSION IF NOT EXISTS citext;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812225133_MakeExerciseNameCaseInsensitive') THEN
    ALTER TABLE "Exercises" ALTER COLUMN "Name" TYPE citext;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812225133_MakeExerciseNameCaseInsensitive') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812225133_MakeExerciseNameCaseInsensitive', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE TABLE "WorkoutTemplates" (
        "Id" uuid NOT NULL,
        "GymId" uuid NOT NULL,
        "Name" citext NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_WorkoutTemplates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE TABLE "WorkoutTemplateDays" (
        "Id" uuid NOT NULL,
        "WorkoutTemplateId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Order" integer NOT NULL,
        CONSTRAINT "PK_WorkoutTemplateDays" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkoutTemplateDays_WorkoutTemplates_WorkoutTemplateId" FOREIGN KEY ("WorkoutTemplateId") REFERENCES "WorkoutTemplates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE TABLE "WorkoutTemplateExercises" (
        "Id" uuid NOT NULL,
        "WorkoutTemplateDayId" uuid NOT NULL,
        "ExerciseId" uuid NOT NULL,
        "Sets" integer NOT NULL,
        "Repetitions" character varying(50) NOT NULL,
        "RestSeconds" integer,
        "Notes" character varying(500),
        "Order" integer NOT NULL,
        CONSTRAINT "PK_WorkoutTemplateExercises" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkoutTemplateExercises_Exercises_ExerciseId" FOREIGN KEY ("ExerciseId") REFERENCES "Exercises" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_WorkoutTemplateExercises_WorkoutTemplateDays_WorkoutTemplat~" FOREIGN KEY ("WorkoutTemplateDayId") REFERENCES "WorkoutTemplateDays" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE INDEX "IX_WorkoutTemplateDays_WorkoutTemplateId" ON "WorkoutTemplateDays" ("WorkoutTemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE UNIQUE INDEX "IX_WorkoutTemplateDays_WorkoutTemplateId_Order" ON "WorkoutTemplateDays" ("WorkoutTemplateId", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE INDEX "IX_WorkoutTemplateExercises_ExerciseId" ON "WorkoutTemplateExercises" ("ExerciseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE INDEX "IX_WorkoutTemplateExercises_WorkoutTemplateDayId" ON "WorkoutTemplateExercises" ("WorkoutTemplateDayId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE UNIQUE INDEX "IX_WorkoutTemplateExercises_WorkoutTemplateDayId_Order" ON "WorkoutTemplateExercises" ("WorkoutTemplateDayId", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE INDEX "IX_WorkoutTemplates_GymId" ON "WorkoutTemplates" ("GymId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    CREATE UNIQUE INDEX "IX_WorkoutTemplates_GymId_Name" ON "WorkoutTemplates" ("GymId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214011_AddWorkoutTemplates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813214011_AddWorkoutTemplates', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE TABLE "Workouts" (
        "Id" uuid NOT NULL,
        "StudentId" uuid NOT NULL,
        "GymId" uuid NOT NULL,
        "SourceWorkoutTemplateId" uuid,
        "Name" character varying(150) NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Workouts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Workouts_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Workouts_WorkoutTemplates_SourceWorkoutTemplateId" FOREIGN KEY ("SourceWorkoutTemplateId") REFERENCES "WorkoutTemplates" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE TABLE "WorkoutDays" (
        "Id" uuid NOT NULL,
        "WorkoutId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Order" integer NOT NULL,
        CONSTRAINT "PK_WorkoutDays" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkoutDays_Workouts_WorkoutId" FOREIGN KEY ("WorkoutId") REFERENCES "Workouts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE TABLE "WorkoutExecutions" (
        "Id" uuid NOT NULL,
        "WorkoutDayId" uuid NOT NULL,
        "CompletedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_WorkoutExecutions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkoutExecutions_WorkoutDays_WorkoutDayId" FOREIGN KEY ("WorkoutDayId") REFERENCES "WorkoutDays" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE TABLE "WorkoutExercises" (
        "Id" uuid NOT NULL,
        "WorkoutDayId" uuid NOT NULL,
        "ExerciseId" uuid NOT NULL,
        "Sets" integer NOT NULL,
        "Repetitions" character varying(50) NOT NULL,
        "RestSeconds" integer,
        "Notes" character varying(500),
        "Order" integer NOT NULL,
        CONSTRAINT "PK_WorkoutExercises" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WorkoutExercises_Exercises_ExerciseId" FOREIGN KEY ("ExerciseId") REFERENCES "Exercises" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_WorkoutExercises_WorkoutDays_WorkoutDayId" FOREIGN KEY ("WorkoutDayId") REFERENCES "WorkoutDays" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_WorkoutDays_WorkoutId" ON "WorkoutDays" ("WorkoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE UNIQUE INDEX "IX_WorkoutDays_WorkoutId_Order" ON "WorkoutDays" ("WorkoutId", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_WorkoutExecutions_CompletedAt" ON "WorkoutExecutions" ("CompletedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_WorkoutExecutions_WorkoutDayId" ON "WorkoutExecutions" ("WorkoutDayId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_WorkoutExercises_ExerciseId" ON "WorkoutExercises" ("ExerciseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_WorkoutExercises_WorkoutDayId" ON "WorkoutExercises" ("WorkoutDayId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE UNIQUE INDEX "IX_WorkoutExercises_WorkoutDayId_Order" ON "WorkoutExercises" ("WorkoutDayId", "Order");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_Workouts_GymId" ON "Workouts" ("GymId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE INDEX "IX_Workouts_SourceWorkoutTemplateId" ON "Workouts" ("SourceWorkoutTemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    CREATE UNIQUE INDEX "IX_Workouts_StudentId" ON "Workouts" ("StudentId") WHERE "IsActive" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814224923_AddWorkouts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814224923_AddWorkouts', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819141521_AddWorkoutExecutionDate') THEN
    ALTER TABLE "WorkoutExecutions" ADD "ExecutionDate" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819141521_AddWorkoutExecutionDate') THEN
    UPDATE "WorkoutExecutions"
    SET "ExecutionDate" = "CompletedAt"::date
    WHERE "ExecutionDate" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819141521_AddWorkoutExecutionDate') THEN
    ALTER TABLE "WorkoutExecutions" ALTER COLUMN "ExecutionDate" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819141521_AddWorkoutExecutionDate') THEN
    CREATE UNIQUE INDEX "IX_WorkoutExecutions_WorkoutDayId_ExecutionDate" ON "WorkoutExecutions" ("WorkoutDayId", "ExecutionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819141521_AddWorkoutExecutionDate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260819141521_AddWorkoutExecutionDate', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821140854_AddPhysicalAssessments') THEN
    CREATE TABLE "PhysicalAssessments" (
        "Id" uuid NOT NULL,
        "StudentId" uuid NOT NULL,
        "AssessmentDate" date NOT NULL,
        "WeightKg" numeric(5,2) NOT NULL,
        "HeightCm" numeric(5,2) NOT NULL,
        "BodyFatPercentage" numeric(5,2),
        "ChestCm" numeric(5,2),
        "WaistCm" numeric(5,2),
        "AbdomenCm" numeric(5,2),
        "HipCm" numeric(5,2),
        "RightArmCm" numeric(5,2),
        "LeftArmCm" numeric(5,2),
        "RightThighCm" numeric(5,2),
        "LeftThighCm" numeric(5,2),
        "RightCalfCm" numeric(5,2),
        "LeftCalfCm" numeric(5,2),
        "Notes" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PhysicalAssessments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PhysicalAssessments_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821140854_AddPhysicalAssessments') THEN
    CREATE INDEX "IX_PhysicalAssessments_StudentId" ON "PhysicalAssessments" ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821140854_AddPhysicalAssessments') THEN
    CREATE UNIQUE INDEX "IX_PhysicalAssessments_StudentId_AssessmentDate" ON "PhysicalAssessments" ("StudentId", "AssessmentDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821140854_AddPhysicalAssessments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260821140854_AddPhysicalAssessments', '10.0.10');
    END IF;
END $EF$;
COMMIT;
