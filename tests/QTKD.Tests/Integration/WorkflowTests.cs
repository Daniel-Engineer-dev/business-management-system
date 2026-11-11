using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Models;

namespace QTKD.Tests.Integration;

[Collection("PostgreSQL API")]
public sealed class WorkflowTests(ApiFixture fixture)
{
    private HttpClient Client() => fixture.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private static object ContractBody(Guid customerId, bool complete = true, string? status = null) => new
    {
        contractNo = $"HD-{Guid.NewGuid():N}", customerId,
        startDate = "2026-01-01", endDate = "2026-12-31",
        contentSummary = complete ? "Dịch vụ Minh Phát" : null,
        attachmentReference = complete ? "minh-phat.pdf" : null, status
    };

    private async Task<Guid> CreateCustomer(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Công ty Minh Phát", status = "Active" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateContract(HttpClient client, bool complete = true)
    {
        var customerId = await CreateCustomer(client);
        var response = await client.PostAsJsonAsync("/api/contracts", ContractBody(customerId, complete));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Draft", (await Json(response)).GetProperty("status").GetString());
        return (await Json(response)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Submit(HttpClient client, Guid id, string key, string role = "Sales")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{id}/submit");
        request.Headers.Add("X-User-Role", role);
        request.Headers.Add("X-User-Id", "sales-week07");
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task TC01_CreateCustomer_PersistsAndCanBeRead()
    {
        using var client = Client();
        var id = await CreateCustomer(client);
        var response = await client.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Công ty Minh Phát", (await Json(response)).GetProperty("name").GetString());
        await using var db = fixture.OpenDatabase();
        Assert.Equal(CustomerStatus.Active, (await db.Customers.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task TC02_EmptyCustomer_IsRejectedWithoutInsert()
    {
        using var client = Client();
        await using var db = fixture.OpenDatabase();
        var count = await db.Customers.CountAsync();
        var response = await client.PostAsJsonAsync("/api/customers", new { name = " ", status = "Active" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).TryGetProperty("errors", out _));
        Assert.Equal(count, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task TC03_EditDraft_PersistsChanges()
    {
        using var client = Client();
        var id = await CreateContract(client, false);
        await using var db = fixture.OpenDatabase();
        var customerId = (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id)).CustomerId;
        var response = await client.PutAsJsonAsync($"/api/contracts/{id}", ContractBody(customerId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Draft", (await Json(response)).GetProperty("status").GetString());
        Assert.Equal("minh-phat.pdf", (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id)).AttachmentReference);
    }

    [Fact]
    public async Task TC04_EditPending_IsRejectedWithoutMutation()
    {
        using var client = Client();
        var id = await CreateContract(client);
        Assert.Equal(HttpStatusCode.OK, (await Submit(client, id, "tc04")).StatusCode);
        await using var db = fixture.OpenDatabase();
        var before = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id);
        var response = await client.PutAsJsonAsync($"/api/contracts/{id}", ContractBody(before.CustomerId, false));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("CTR-01", (await Json(response)).GetProperty("title").GetString());
        var after = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(before.ContentSummary, after.ContentSummary);
        Assert.Equal(before.AttachmentReference, after.AttachmentReference);
        Assert.Equal(ContractStatus.PendingApproval, after.Status);
    }

    [Fact]
    public async Task TC05_MissingSubmissionData_ReportsErrorsAndKeepsDraft()
    {
        using var client = Client();
        var id = await CreateContract(client, false);
        var response = await Submit(client, id, "tc05");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("CTR_02_FAILED", body.GetProperty("code").GetString());
        Assert.Equal(2, body.GetProperty("unmetConditions").GetArrayLength());
        await using var db = fixture.OpenDatabase();
        Assert.Equal(ContractStatus.Draft, (await db.Contracts.SingleAsync(x => x.Id == id)).Status);
        Assert.False(await db.ApprovalRequests.AnyAsync(x => x.ContractId == id));
        Assert.False(await db.ContractHistory.AnyAsync(x => x.ContractId == id));
    }

    [Fact]
    public async Task TC06_FullWeek06Demo_RepairThenSubmitAndReadState()
    {
        using var client = Client();
        var id = await CreateContract(client, false);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(client, id, "tc06")).StatusCode);
        await using var db = fixture.OpenDatabase();
        var customerId = (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id)).CustomerId;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/contracts/{id}", ContractBody(customerId))).StatusCode);
        var response = await Submit(client, id, "tc06");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("PendingApproval", body.GetProperty("status").GetString());
        var approvalId = body.GetProperty("approvalId").GetGuid();
        var read = await client.GetAsync($"/api/contracts/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(approvalId, (await Json(read)).GetProperty("approvalId").GetGuid());
        Assert.Equal("PendingApproval", (await Json(read)).GetProperty("status").GetString());
        Assert.Equal(ContractStatus.PendingApproval, (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(approvalId, (await db.ApprovalRequests.SingleAsync(x => x.ContractId == id)).Id);
        Assert.Equal("Submitted", (await db.ContractHistory.SingleAsync(x => x.ContractId == id)).Action);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task TC07_DirectApproved_IsExplicitlyRejected(string method)
    {
        using var client = Client();
        var id = await CreateContract(client);
        await using var db = fixture.OpenDatabase();
        var before = await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id);
        var count = await db.Contracts.CountAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), method == "POST" ? "/api/contracts" : $"/api/contracts/{id}")
        {
            Content = JsonContent.Create(ContractBody(before.CustomerId, status: "Approved"))
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONTRACT_STATE_INVALID", (await Json(response)).GetProperty("code").GetString());
        Assert.Equal(count, await db.Contracts.CountAsync());
        Assert.Equal(ContractStatus.Draft, (await db.Contracts.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task TC08_RepeatedSubmit_CreatesOneApprovalAndHistory()
    {
        using var client = Client();
        var id = await CreateContract(client);
        var first = await Submit(client, id, "tc08");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await Json(first);
        Assert.False(firstBody.GetProperty("idempotentReplay").GetBoolean());
        var replay = await Submit(client, id, "tc08");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await Json(replay);
        Assert.Equal(firstBody.GetProperty("approvalId").GetGuid(), body.GetProperty("approvalId").GetGuid());
        Assert.True(body.GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, id, "different-key")).StatusCode);
        await using var db = fixture.OpenDatabase();
        Assert.Equal(1, await db.ApprovalRequests.CountAsync(x => x.ContractId == id));
        Assert.Equal(1, await db.ContractHistory.CountAsync(x => x.ContractId == id));
    }

    [Fact]
    public async Task ConcurrentSubmit_SameKeyCreatesOneResult()
    {
        using var client = Client();
        var id = await CreateContract(client);
        var responses = await Task.WhenAll(Submit(client, id, "concurrent"), Submit(client, id, "concurrent"));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal((await Json(responses[0])).GetProperty("approvalId").GetGuid(),
            (await Json(responses[1])).GetProperty("approvalId").GetGuid());
        await using var db = fixture.OpenDatabase();
        Assert.Equal(1, await db.ApprovalRequests.CountAsync(x => x.ContractId == id));
        Assert.Equal(1, await db.ContractHistory.CountAsync(x => x.ContractId == id));
    }

    [Fact]
    public async Task UnauthorizedSubmit_HasNoSideEffects()
    {
        using var client = Client();
        var id = await CreateContract(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Submit(client, id, "unauthorized", "Viewer")).StatusCode);
        await using var db = fixture.OpenDatabase();
        Assert.Equal(ContractStatus.Draft, (await db.Contracts.SingleAsync(x => x.Id == id)).Status);
        Assert.False(await db.ApprovalRequests.AnyAsync(x => x.ContractId == id));
    }

    [Fact]
    public async Task Pricing_ValidItemPersists_NegativeAndDuplicateAreRejected()
    {
        using var client = Client();
        var response = await client.PostAsJsonAsync("/api/price-lists", new { name = "Tuần 7", effectiveDate = "2026-11-04" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await Json(response)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/price-lists/{id}/items", new { serviceCode = "DV01", unitPrice = -1 })).StatusCode);
        var created = await client.PostAsJsonAsync($"/api/price-lists/{id}/items", new { serviceCode = "dv01", unitPrice = 150000 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("DV01", (await Json(created)).GetProperty("serviceCode").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/price-lists/{id}/items", new { serviceCode = "DV01", unitPrice = 10 })).StatusCode);
        var read = await client.GetAsync($"/api/price-lists/{id}/items");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(1, (await Json(read)).GetArrayLength());
        await using var db = fixture.OpenDatabase();
        Assert.Equal(150000m, (await db.PriceItems.SingleAsync(x => x.PriceListId == id)).UnitPrice);
    }
}
