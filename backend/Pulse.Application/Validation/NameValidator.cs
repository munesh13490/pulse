namespace Pulse.Application.Validation;

public static class NameValidator
{
    public static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new ArgumentException("Name must contain 1-200 characters.");
    }
}
