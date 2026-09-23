using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Предложения по ВНД — см. VndProposal.</summary>
public interface IVndProposalService
{
    /// <summary>Отправить предложение (любой пользователь, которому виден ВНД).
    /// Получатели (право ManageVndProposals) получают уведомление в системе и письмо.</summary>
    Task<VndProposalCreatedResponse> CreateAsync(
        int vndId, CreateVndProposalRequest request, IReadOnlyList<IFormFile> files, int currentUserId,
        CancellationToken ct = default);

    Task<VndProposalPagedResponse> SearchAsync(VndProposalFilterRequest filter, CancellationToken ct = default);
    Task<VndProposalResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<VndProposalCountsResponse> GetCountsAsync(CancellationToken ct = default);

    Task<VndProposalResponse> MarkAsReadAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<VndProposalResponse> MarkAsUnreadAsync(int id, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(int currentUserId, CancellationToken ct = default);
}
