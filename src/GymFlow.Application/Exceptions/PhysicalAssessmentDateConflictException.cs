namespace GymFlow.Application.Exceptions;

public sealed class PhysicalAssessmentDateConflictException
    : Exception
{
    public PhysicalAssessmentDateConflictException(
        Exception innerException)
        : base(
            "Já existe uma avaliação física para este aluno nesta data.",
            innerException)
    {
    }
}
