using QTKD.Api.Models;
using QTKD.Api.Services;

namespace QTKD.Tests.Unit;

public sealed class ContractRulesTests
{
    [Theory]
    [InlineData(ContractStatus.Draft, true)]
    [InlineData(ContractStatus.RevisionRequested, true)]
    [InlineData(ContractStatus.PendingApproval, false)]
    [InlineData(ContractStatus.Approved, false)]
    [InlineData(ContractStatus.Active, false)]
    [InlineData(ContractStatus.Rejected, false)]
    public void CTR01_EditRequiresEditableState(ContractStatus status, bool expected) =>
        Assert.Equal(expected, ContractRules.CanEdit(status));

    [Fact]
    public void CTR02_CompleteContractHasNoErrors() =>
        Assert.Empty(ContractRules.ValidateSubmission(ValidContract()));

    [Theory]
    [InlineData("customer")]
    [InlineData("inactive")]
    [InlineData("name")]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("dates")]
    [InlineData("content")]
    [InlineData("attachment")]
    public void CTR02_MissingConditionIsRejected(string condition)
    {
        var contract = ValidContract();
        switch (condition)
        {
            case "customer": contract.Customer = null!; break;
            case "inactive": contract.Customer.Status = CustomerStatus.Inactive; break;
            case "name": contract.Customer.Name = " "; break;
            case "start": contract.StartDate = default; break;
            case "end": contract.EndDate = default; break;
            case "dates": contract.EndDate = contract.StartDate.AddDays(-1); break;
            case "content": contract.ContentSummary = " "; break;
            case "attachment": contract.AttachmentReference = null; break;
        }
        Assert.NotEmpty(ContractRules.ValidateSubmission(contract));
        Assert.Equal(ContractStatus.Draft, contract.Status);
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.PendingApproval, true)]
    [InlineData(ContractStatus.Draft, ContractStatus.Approved, false)]
    [InlineData(ContractStatus.PendingApproval, ContractStatus.PendingApproval, false)]
    public void CTR03_OnlyDraftToPendingApprovalIsSupported(
        ContractStatus from, ContractStatus to, bool expected) =>
        Assert.Equal(expected, ContractRules.CanTransition(from, to));

    private static Contract ValidContract() => new()
    {
        Id = Guid.NewGuid(), ContractNo = "HD-UNIT", Status = ContractStatus.Draft,
        StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
        ContentSummary = "Dịch vụ", AttachmentReference = "contract.pdf",
        Customer = new Customer { Name = "Minh Phát", Status = CustomerStatus.Active }
    };
}
