using System.ComponentModel.DataAnnotations;

public class PaymentOptions
{
    [Required]
    public required string GatewayUrl { get; init; }
    [Range(100, 10000)]
    public decimal maxDepositBirr { get; init; }
}