namespace generator;

public record Formula(string name, string full_name, string tap, string desc, string homepage, DateTime date_added, string? readme);

public record PageData<T>(int totalPages, int current, bool isLatest, IEnumerable<T> items);