namespace NimbosService.DTOs;

public record MilestoneAwardDTO(
    int MilestoneShards,
    string? Award1,
    string? Award2,
    string? Award3,
    int? ClaimedAwardIndex,
    DateTime? ClaimedAt,
    DateTime? ParentViewedAt
);

public record SetMilestoneAwardRequest(
    string? Award1,
    string? Award2,
    string? Award3
);

public record ClaimAwardRequest(int AwardIndex);  // 1, 2, or 3
