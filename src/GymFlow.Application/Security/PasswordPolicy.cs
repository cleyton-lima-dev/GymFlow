namespace GymFlow.Application.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static void Validate(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException(
                "A senha é obrigatória.");
        }

        if (password.Length < MinimumLength)
        {
            throw new ArgumentException(
                $"A senha deve ter pelo menos {MinimumLength} caracteres.");
        }

        if (password.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"A senha deve ter no máximo {MaximumLength} caracteres.");
        }
    }
}