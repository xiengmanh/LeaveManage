using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LeaveManage.Tests;

[Collection("api")]
public class LeaveAttachmentTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n");
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly ApiFixture _api;
    public LeaveAttachmentTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Owner_UploadsPdf_ListsAndDownloadsSameBytes()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(20));

        var uploaded = await UploadAsync(owner, requestId, "giay-kham.pdf", "application/pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var attachmentId = (await Json(uploaded)).GetProperty("id").GetInt32();

        var list = await owner.GetAsync($"/api/leave-requests/{requestId}/attachments");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = await Json(list);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("giay-kham.pdf", items[0].GetProperty("fileName").GetString());

        var download = await owner.GetAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_PngAndJpeg_AreAccepted()
    {
        var owner = await _api.LoginAsync("nv004");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(21));

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(owner, requestId, "anh.png", "image/png", PngBytes)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(owner, requestId, "anh.jpg", "image/jpeg", JpegBytes)).StatusCode);
    }

    [Fact]
    public async Task Upload_WrongMagicBytes_IsRejected()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(22));

        // Đuôi .pdf nhưng nội dung là file thực thi/văn bản thường
        var response = await UploadAsync(owner, requestId, "gia-mao.pdf", "application/pdf", Encoding.ASCII.GetBytes("MZ-not-a-pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_FILE_CONTENT", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Upload_DisallowedExtension_IsRejected()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(23));

        var response = await UploadAsync(owner, requestId, "malware.exe", "application/octet-stream", PdfBytes);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_FILE_TYPE", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Upload_OverFiveMegabytes_IsRejected()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(24));

        var big = new byte[5 * 1024 * 1024 + 1];
        PdfBytes.CopyTo(big, 0);
        var response = await UploadAsync(owner, requestId, "lon.pdf", "application/pdf", big);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Upload_SixthFile_IsRejected()
    {
        var owner = await _api.LoginAsync("nv004");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(25));

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await UploadAsync(owner, requestId, $"tep-{i}.pdf", "application/pdf", PdfBytes)).StatusCode);

        var sixth = await UploadAsync(owner, requestId, "tep-6.pdf", "application/pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.BadRequest, sixth.StatusCode);
        Assert.Equal("TOO_MANY_ATTACHMENTS", (await Json(sixth)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task DirectManager_CanListAndDownload_UnrelatedUser_Gets404()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(26));
        var attachmentId = (await Json(await UploadAsync(owner, requestId, "xem.pdf", "application/pdf", PdfBytes))).GetProperty("id").GetInt32();

        // "manager" (NV002) là quản lý trực tiếp của nv003
        var manager = await _api.LoginAsync("manager");
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/leave-requests/{requestId}/attachments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}")).StatusCode);

        // nv004 không liên quan đến đơn của nv003
        var unrelated = await _api.LoginAsync("nv004");
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.GetAsync($"/api/leave-requests/{requestId}/attachments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.GetAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Manager_CannotUploadToSubordinateRequest()
    {
        var owner = await _api.LoginAsync("nv003");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(27));

        var manager = await _api.LoginAsync("manager");
        var response = await UploadAsync(manager, requestId, "thay.pdf", "application/pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_AfterApproval_IsBlocked()
    {
        var owner = await _api.LoginAsync("nv005");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(28));
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/leave-requests/{requestId}/submit", null)).StatusCode);

        // nv005 do admin (employee 1) quản lý
        var admin = await _api.LoginAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/leave-requests/{requestId}/approve", new { comment = "ok" })).StatusCode);

        var response = await UploadAsync(owner, requestId, "muon.pdf", "application/pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATUS", (await Json(response)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Upload_AfterCancel_IsBlocked()
    {
        var owner = await _api.LoginAsync("nv006");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(29));
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/leave-requests/{requestId}/cancel", new { reason = "hủy" })).StatusCode);

        var response = await UploadAsync(owner, requestId, "huy.pdf", "application/pdf", PdfBytes);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanDeletePendingAttachment_ButNotAfterApproval()
    {
        var owner = await _api.LoginAsync("nv006");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(30));
        var attachmentId = (await Json(await UploadAsync(owner, requestId, "xoa.pdf", "application/pdf", PdfBytes))).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/leave-requests/{requestId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Manager_CannotDeleteSubordinateAttachment()
    {
        var owner = await _api.LoginAsync("nv004");
        var requestId = await CreateDraftAsync(owner, 1, ApiFixture.Weekday(31));
        var attachmentId = (await Json(await UploadAsync(owner, requestId, "giu.pdf", "application/pdf", PdfBytes))).GetProperty("id").GetInt32();

        var manager = await _api.LoginAsync("manager");
        var response = await manager.DeleteAsync($"/api/leave-requests/{requestId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- helpers ----------

    private static async Task<int> CreateDraftAsync(HttpClient client, int leaveTypeId, DateTime day)
    {
        var created = await client.PostAsJsonAsync("/api/leave-requests", new
        {
            leaveTypeId,
            startDate = day,
            endDate = day,
            reason = "Kiểm tra đính kèm"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await Json(created)).GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, int requestId, string fileName, string contentType, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return await client.PostAsync($"/api/leave-requests/{requestId}/attachments", form);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFixture.Json);
}
