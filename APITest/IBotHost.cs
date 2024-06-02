namespace APITest
{
    public interface IBotHost
    {
        Task StartAsync();

        Task StopAsync();
    }
}