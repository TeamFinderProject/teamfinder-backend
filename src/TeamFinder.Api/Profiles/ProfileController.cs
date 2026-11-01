using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamFinder.Api.Common;

namespace TeamFinder.Api.Profiles;

[ApiController]
[Authorize]
[Route("api/profile")]
[Produces("application/json")]
public sealed class ProfileController(ProfileService profiles) : ControllerBase
{
    [HttpGet]
    public Task<ProfileDto> Get(CancellationToken ct) => profiles.GetAsync(User.GetUserId(), ct);

    [HttpPut]
    public Task<ProfileDto> Update(UpdateProfileRequest request, CancellationToken ct) =>
        profiles.UpdateAsync(User.GetUserId(), request, ct);
}
