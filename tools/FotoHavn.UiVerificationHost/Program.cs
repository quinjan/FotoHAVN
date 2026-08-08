namespace FotoHavn.UiVerificationHost;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var options = HostOptions.Parse(args);
            return new VerificationRunner(options).Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }
}
