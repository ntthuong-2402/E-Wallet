namespace Friday.Modules.Admin.Application.Security;

public interface IPasswordPolicy
{
    void Validate(string password);
}
