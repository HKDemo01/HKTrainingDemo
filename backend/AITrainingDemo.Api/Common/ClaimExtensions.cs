using System.Security.Claims;

namespace AITrainingDemo.Api.Common;

/// <summary>
/// 从当前请求的 JWT 中读取登录用户 id（claim 名 "sub"，注意 JWT 配置关闭了入站声明映射）。
/// </summary>
public static class ClaimExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");
        if (!int.TryParse(value, out var userId))
        {
            // 缺失或非数字的 sub 属于非法身份声明，转 401 而不是让 int.Parse 抛 500
            throw new BusinessException(401, "登录状态无效，请重新登录");
        }

        return userId;
    }
}
