namespace NimbosService.DTOs;

public record CreateTaskRequest(string Title);

public record UpdateTaskRequest(
    bool? IsCompleted,
    bool? IsSnoozed,
    bool? IsDismissedToday,
    bool? IsSkippedTomorrow,
    string? Title
);

public record TaskDTO(
    Guid Id,
    string Title,
    bool IsCompleted,
    bool IsSnoozed,
    bool IsDismissedToday,
    bool IsSkippedTomorrow,
    bool IsTomorrowOnly,
    bool AddedByParent
);

public record UpdateTaskResponse(TaskDTO Task, UserStarsDTO User);

public record UserStarsDTO(int TotalStars, int DailyStars);
