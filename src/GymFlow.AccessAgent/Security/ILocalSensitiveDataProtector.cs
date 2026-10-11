namespace GymFlow.AccessAgent.Security;

public interface ILocalSensitiveDataProtector
{
    string Protect(string value);

    string Unprotect(string protectedValue);

    string ComputeLookupHash(
        string providerKey,
        int credentialType,
        string externalIdentifier);
}