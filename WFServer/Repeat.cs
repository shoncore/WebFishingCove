using System.Timers;

namespace WFServer
{
    public class Repeat
    {
        private Func<int> cb;
        public int msBetweenCalls = 1000;
        private System.Timers.Timer timer;

        public Repeat(Func<int> callback, int delay)
        {
            cb = callback;
            msBetweenCalls = delay;
            timer = new System.Timers.Timer(msBetweenCalls);
            timer.Elapsed += onElapsed;
        }

        private void onElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            cb();
        }

        public void Start()
        {
            timer.AutoReset = true;
            timer.Enabled = true; // start the timer!
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }
    }
}
