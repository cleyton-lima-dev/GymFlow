namespace GymFlow.Application.Validation;

public static class GymTimeZonePolicy
{
    public static string ValidateIana(string value)
    {
        var id = value.Trim();
        if (id.Length is 0 or > 100 || !TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))
            throw new ArgumentException("Informe um fuso horário IANA válido, como America/Sao_Paulo.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Fuso horário indisponível no servidor."); }
        return id;
    }
    public static string ToIana(string id) => TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _) ? id
        : TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) ? iana : id;
}
