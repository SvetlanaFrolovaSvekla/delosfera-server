using delosfera_server.Modules.Vnd.Models;

namespace delosfera_server.Modules.Vnd.DTO.Request;

public class StartApprovalRequest
{
    public required List<ApprovalStageRequest> Stages { get; set; }

    public required int PrimaryDeadlineHours { get; set; }
    public required int RepeatDeadlineHours { get; set; }
    public required int FinalHoldDeadlineHours { get; set; }
}

public class ApprovalStageRequest
{
    public required ApprovalStageKind Kind { get; set; }
    public required int ApproverUserId { get; set; }
}