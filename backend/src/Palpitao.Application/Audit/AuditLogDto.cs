using Palpitao.Application.AdminPredictions;
using Palpitao.Application.Flavio;
using Palpitao.Application.Ocr;
using Palpitao.Application.Registrations;
using Palpitao.Application.Users;

namespace Palpitao.Application.Audit;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
