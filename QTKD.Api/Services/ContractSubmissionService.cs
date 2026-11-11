using Microsoft.EntityFrameworkCore;
using QTKD.Api.Contracts;
using QTKD.Api.Data;
using QTKD.Api.Models;

namespace QTKD.Api.Services;

public sealed class ContractSubmissionService(QtkdDbContext dbContext)
{
    public async Task<SubmitContractResult> SubmitAsync(
        Guid contractId,
        string idempotencyKey,
        string submittedBy,
        CancellationToken cancellationToken)
    {
        var replay = await dbContext.ApprovalRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.ContractId == contractId && request.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (replay is not null)
        {
            return SubmitContractResult.Success(new SubmitContractResponse(
                contractId,
                ContractStatus.PendingApproval.ToString(),
                replay.Id,
                true));
        }

        var contract = await dbContext.Contracts
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == contractId, cancellationToken);

        if (contract is null)
        {
            return SubmitContractResult.NotFound();
        }

        if (!ContractRules.CanTransition(contract.Status, ContractStatus.PendingApproval))
        {
            return SubmitContractResult.Conflict(
                "CONTRACT_STATE_INVALID",
                $"Chỉ hợp đồng ở trạng thái Draft mới được gửi. Trạng thái hiện tại: {contract.Status}.");
        }

        var unmetConditions = ContractRules.ValidateSubmission(contract);
        if (unmetConditions.Count > 0)
        {
            return SubmitContractResult.Invalid(unmetConditions);
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            IdempotencyKey = idempotencyKey,
            SubmittedBy = submittedBy,
            SubmittedAtUtc = now,
            Status = ApprovalRequestStatus.Pending
        };

        contract.Status = ContractStatus.PendingApproval;
        dbContext.ApprovalRequests.Add(approval);
        dbContext.ContractHistory.Add(new ContractHistory
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Action = "Submitted",
            PerformedBy = submittedBy,
            OccurredAtUtc = now,
            Details = $"Draft -> PendingApproval; ApprovalId={approval.Id}"
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentRequest = await dbContext.ApprovalRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(request => request.ContractId == contractId, cancellationToken);

            if (concurrentRequest is not null && concurrentRequest.IdempotencyKey == idempotencyKey)
            {
                return SubmitContractResult.Success(new SubmitContractResponse(
                    contractId,
                    ContractStatus.PendingApproval.ToString(),
                    concurrentRequest.Id,
                    true));
            }

            if (concurrentRequest is not null)
            {
                return SubmitContractResult.Conflict(
                    "CONTRACT_ALREADY_SUBMITTED",
                    "Hợp đồng đã được gửi bởi một yêu cầu khác; không tạo thêm phiên phê duyệt.");
            }

            throw;
        }

        return SubmitContractResult.Success(new SubmitContractResponse(
            contract.Id,
            contract.Status.ToString(),
            approval.Id,
            false));
    }


}

public sealed record SubmitContractResult(
    SubmitContractResultKind Kind,
    SubmitContractResponse? Response = null,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyList<string>? UnmetConditions = null)
{
    public static SubmitContractResult Success(SubmitContractResponse response) =>
        new(SubmitContractResultKind.Success, Response: response);

    public static SubmitContractResult NotFound() => new(SubmitContractResultKind.NotFound);

    public static SubmitContractResult Conflict(string code, string message) =>
        new(SubmitContractResultKind.Conflict, ErrorCode: code, Message: message);

    public static SubmitContractResult Invalid(IReadOnlyList<string> conditions) =>
        new(SubmitContractResultKind.Invalid, ErrorCode: "CTR_02_FAILED",
            Message: "Hợp đồng chưa đủ điều kiện gửi trình duyệt.", UnmetConditions: conditions);
}

public enum SubmitContractResultKind
{
    Success,
    NotFound,
    Invalid,
    Conflict
}
