namespace DBTool.Config;

public static class FirebirdDefaults
{
    public const string DefaultDatabaseFileName = "database.fdb";

    public const string Host = "127.0.0.1";
    public const int Port = 3050;
    public const string User = "SYSDBA";

    public const string EnvHost = "FB_HOST";
    public const string EnvPort = "FB_PORT";
    public const string EnvUser = "FB_USER";
    public const string EnvPassword = "FB_PASSWORD";

    public const string DefaultCharset = "UTF8";
    public const int DefaultDialect = 3;
}
