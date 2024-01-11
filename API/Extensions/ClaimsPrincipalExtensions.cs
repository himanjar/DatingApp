using System.Security.Claims;

namespace API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUsername(this ClaimsPrincipal user)
        {
            // Get the username from the token
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

    }
}
