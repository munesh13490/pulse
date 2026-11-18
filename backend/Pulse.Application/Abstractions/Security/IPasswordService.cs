namespace Pulse.Application.Abstractions.Security;

public interface IPasswordService
{
    string Hash(string username, string password);
}
