namespace Pulse.Application.Validation;

public static class UserValidator
{
    public static void Validate(Guid? tenant, string name, string password, string role)
    {
        NameValidator.Validate(name);
        if (string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 256)
            throw new ArgumentException("Password must contain 12-256 characters.");
        if (role is not ("platform-admin" or "tenant-admin" or "operator" or "viewer"))
            throw new ArgumentException("Invalid role");
        if ((role == "platform-admin") != (tenant is null))
            throw new ArgumentException("Platform administrators have no tenant; tenant users require one.");
    }
}
