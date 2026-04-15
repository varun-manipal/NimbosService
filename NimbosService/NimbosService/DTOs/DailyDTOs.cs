namespace NimbosService.DTOs;

public record NewDayRequest(string LastOpenedDate, string? CurrentDate);

public record NewDayResponse(
    bool WasNewDay,
    SnapshotDTO? Snapshot,
    ShieldDTO Shield,
    List<TaskDTO> Tasks
);

public record SnapshotDTO(string Date, double CompletionPercentage, int StarsLit);
