using TeamFinder.Api.Entities;
using TeamFinder.Api.Notifications;

namespace TeamFinder.Api.Applications;

/// <summary>
/// Машина состояний заявки (раздел 13.1 контракта, RULE-APP-*).
///
///   PENDING ──accept──▶ ACCEPTED
///   PENDING ──reject──▶ REJECTED
///   PENDING ──withdraw▶ WITHDRAWN
///   PENDING ──система─▶ CLOSED (closeReason)
///
/// Все переходы — только из PENDING. Остальные состояния конечные.
/// </summary>
public static class ApplicationRules
{
    public static bool IsFinal(ApplicationStatus status) => status != ApplicationStatus.Pending;

    /// <summary>Системное закрытие ожидающих заявок с причиной и уведомлением автору.</summary>
    public static void Close(IEnumerable<Application> pending, ApplicationCloseReason reason, string projectTitle,
        DateTimeOffset now, NotificationService notifications)
    {
        foreach (var application in pending.Where(a => a.Status == ApplicationStatus.Pending))
        {
            application.Status = ApplicationStatus.Closed;
            application.ClosureReason = reason;
            application.DecidedAt = now;
            notifications.Add(application.UserId, NotificationType.ApplicationClosed,
                $"Заявка в проект «{projectTitle}» закрыта: {Describe(reason)}", application.ProjectId);
        }
    }

    public static string Describe(ApplicationCloseReason reason) => reason switch
    {
        ApplicationCloseReason.AcceptedAnotherRole => "вас приняли на другую роль этого проекта",
        ApplicationCloseReason.RoleFilled => "все места на роли заняты",
        ApplicationCloseReason.RoleClosed => "владелец закрыл набор на эту роль",
        ApplicationCloseReason.RecruitmentClosed => "набор в проект завершён",
        ApplicationCloseReason.ProjectCancelled => "проект отменён",
        _ => "заявка закрыта",
    };
}
