using System.Text.Json;
using CFTools.Models;
using Xunit;

namespace CFTools.Tests;

public class ApiResponseTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void TokenVerify_MessagesAreObjects_Deserializes()
    {
        // Real shape of GET user/tokens/verify: messages carry {code, message}, not strings.
        const string json = """
            {"result":{"id":"abc123","status":"active"},
             "success":true,
             "errors":[],
             "messages":[{"code":10000,"message":"This API Token is valid and active","type":null}]}
            """;

        var data = JsonSerializer.Deserialize<ApiResponse<CfTokenVerifyResult>>(json, Options);

        Assert.NotNull(data);
        Assert.True(data!.Success);
        Assert.Equal("active", data.Result.Status);
        Assert.Equal("This API Token is valid and active", data.Messages![0].Message);
    }

    [Fact]
    public void Response_WithoutMessages_Deserializes()
    {
        const string json = """{"result":[],"success":true,"errors":[]}""";

        var data = JsonSerializer.Deserialize<ApiResponse<List<CfAccount>>>(json, Options);

        Assert.NotNull(data);
        Assert.Null(data!.Messages);
    }
}
