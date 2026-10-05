namespace GymFlow.Domain.Enums;

public enum PhysicalAccessDecisionReason
{
    Eligible = 1,
    ManualOverride = 2,

    CredentialNotFound = 100,
    CredentialInactive = 101,
    StudentInactive = 102,
    StudentArchived = 103,
    NoValidEnrollment = 104,
    FinancialRestriction = 105,
    ManualBlock = 106,
    PlanRestriction = 107
}