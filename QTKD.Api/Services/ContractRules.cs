using QTKD.Api.Models;

namespace QTKD.Api.Services;

public static class ContractRules
{
    public static bool CanEdit(ContractStatus status) =>
        status is ContractStatus.Draft or ContractStatus.RevisionRequested;

    public static bool CanSubmit(ContractStatus status) => status == ContractStatus.Draft;

    public static bool CanTransition(ContractStatus from, ContractStatus to) =>
        CanSubmit(from) && to == ContractStatus.PendingApproval;

    public static List<string> ValidateSubmission(Contract contract)
    {
        var errors = new List<string>();

        if (contract.Customer is null || contract.Customer.Status != CustomerStatus.Active ||
            string.IsNullOrWhiteSpace(contract.Customer.Name))
        {
            errors.Add("Customer phải tồn tại, có tên và ở trạng thái Active.");
        }

        if (contract.StartDate == default || contract.EndDate == default || contract.EndDate < contract.StartDate)
        {
            errors.Add("Thời hạn hiệu lực phải có đủ ngày bắt đầu/kết thúc và ngày kết thúc không trước ngày bắt đầu.");
        }

        if (string.IsNullOrWhiteSpace(contract.ContentSummary))
        {
            errors.Add("Thiếu nội dung bắt buộc (giả định demo: ContentSummary).");
        }

        if (string.IsNullOrWhiteSpace(contract.AttachmentReference))
        {
            errors.Add("Thiếu tệp bắt buộc (giả định demo: AttachmentReference).");
        }

        return errors;
    }
}
