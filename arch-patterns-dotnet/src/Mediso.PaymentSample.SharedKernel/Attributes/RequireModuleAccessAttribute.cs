namespace Mediso.PaymentSample.SharedKernel.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireModuleAccessAttribute : Attribute
{
    public string To { get; }
    public string Operation { get; }
    public RequireModuleAccessAttribute(string to, string operation)
        => (To, Operation) = (to, operation);
}