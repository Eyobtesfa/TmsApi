namespace TmsApi.Infrastructure.Caching;

public static class CacheKeys
{
    private const string SchemaVersion = "v2";
    public static string Course(string code) => $"{SchemaVersion}:course:{code}";
    public static string CoursesAll => $"{SchemaVersion}:courses:all";

    public static string Courses(int page, int pageSize, string? search, string orderBy, bool descending) =>
       $"{SchemaVersion}:courses:page={page}:size={pageSize}" +
       $":search={search ?? ""}:order={orderBy}:desc={descending}";
    public const string CoursesTag = "courses";
}

