using Microsoft.Extensions.Options;

namespace delosfera_server.Common.Security;

/// <summary>
/// Парольная политика банка (NFR-03). Значения задаются конфигурацией: требования
/// службы информационной безопасности меняются, и пересборка ради длины пароля —
/// не тот процесс, которым это должно решаться.
/// </summary>
public class PasswordPolicyOptions
{
    public const string Section = "Security:PasswordPolicy";

    public int MinLength { get; set; } = 10;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;

    /// <summary>Сколько неудачных попыток подряд до временной блокировки входа.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>На сколько минут запирается вход после исчерпания попыток.</summary>
    public int LockoutMinutes { get; set; } = 15;
}

public interface IPasswordPolicy
{
    /// <summary>Проверяет пароль и бросает исключение с перечнем невыполненных требований.</summary>
    void Validate(string password);

    int MaxFailedAttempts { get; }
    TimeSpan LockoutDuration { get; }
}

/// <summary>
/// Проверка пароля по политике.
///
/// Сообщение перечисляет все невыполненные требования сразу: сообщать про них по
/// одному — значит заставлять сотрудника угадывать пароль с пятой попытки.
/// </summary>
public class PasswordPolicy : IPasswordPolicy
{
    private readonly PasswordPolicyOptions _options;

    public PasswordPolicy(IOptions<PasswordPolicyOptions> options) => _options = options.Value;

    public int MaxFailedAttempts => _options.MaxFailedAttempts;

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(_options.LockoutMinutes);

    public void Validate(string password)
    {
        var problems = new List<string>();

        if (string.IsNullOrEmpty(password) || password.Length < _options.MinLength)
            problems.Add($"не короче {_options.MinLength} символов");

        if (_options.RequireUppercase && !password.Any(char.IsUpper))
            problems.Add("заглавная буква");

        if (_options.RequireLowercase && !password.Any(char.IsLower))
            problems.Add("строчная буква");

        if (_options.RequireDigit && !password.Any(char.IsDigit))
            problems.Add("цифра");

        if (_options.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            problems.Add("специальный символ");

        if (problems.Count == 0) return;

        throw new InvalidOperationException(
            "Пароль не соответствует требованиям безопасности: " + string.Join(", ", problems));
    }
}
