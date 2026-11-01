using System.ComponentModel.DataAnnotations;

namespace TeamFinder.Api.Common;

/// <summary>Пустое значение допустимо; иначе — абсолютный http(s) URL.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AbsoluteHttpUrlAttribute : ValidationAttribute
{
    public AbsoluteHttpUrlAttribute() : base("Укажите ссылку целиком, например https://t.me/team") { }

    public override bool IsValid(object? value) =>
        value is not string s
        || string.IsNullOrWhiteSpace(s)
        || (Uri.TryCreate(s, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
