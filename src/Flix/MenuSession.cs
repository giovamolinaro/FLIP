using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Flix;

/// <summary>Un processo, una richiesta, la finestra M4. La chiusura della finestra termina il processo.</summary>
internal static class MenuSession
{
    internal static int ShowNotice(string message)
    {
        return Pump(session =>
        {
            session.Code = 1;
            session.Present(MenuProgressModel.Reject("", "", message, busy: false));
            session.MarkJobFinished();
        });
    }

    internal static int Run(MenuConversion.Request request)
    {
        return Pump(session => { _ = session.Execute(request); });
    }

    static int Pump(Action<Session> start)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var session = new Session(application);
        application.Startup += (_, _) => start(session);
        application.Run();
        return session.Code;
    }

    sealed class Session
    {
        readonly Application application;
        int windowClosed;
        int jobDone;
        internal int Code = 1;
        internal CancellationTokenSource? Job { get; private set; }
        internal MenuProgressWindow? Window { get; private set; }

        internal Session(Application application) { this.application = application; }

        internal void BindJob(CancellationTokenSource? job) => Job = job;

        internal async Task Execute(MenuConversion.Request request)
        {
            bool busy = false;
            try
            {
                var gate = new MenuProgressGate(this);
                var outcome = await MenuConversion.RunExclusive(
                    () => busy,
                    value => busy = value,
                    () => Job,
                    BindJob,
                    request,
                    (path, format, token) => ConvertOne(path, format, token, (percent, _) => gate.Report(percent)),
                    message => gate.Notice(request, message),
                    onCompleted: gate.Complete).ConfigureAwait(true);
                Code = outcome.Succeeded ? 0 : 1;
            }
            catch (Exception ex)
            {
                Code = 1;
                Present(MenuProgressModel.Reject(NameOf(request.File), MenuProgressRouter.DisplayFormat(request.Format), ex.Message, busy: false));
            }
            finally
            {
                MarkJobFinished();
            }
        }

        static async Task<ConversionResult> ConvertOne(string path, string format, CancellationToken token, Action<int, string>? percentProgress)
        {
            var results = await Task.Run(() => ConversionEngine.Convert([path], [format], token, null, percentProgress), token).ConfigureAwait(false);
            return results.Count == 1 ? results[0] : new ConversionResult(path, format, [], "Conversione non riuscita.");
        }

        internal void Present(MenuProgressModel model)
        {
            if (!application.Dispatcher.CheckAccess())
            {
                application.Dispatcher.Invoke(() => Present(model));
                return;
            }
            if (application.Dispatcher.HasShutdownStarted) return;
            if (Window != null)
            {
                Window.Refresh();
                return;
            }
            var opened = MenuProgressWindow.ShowLive(model);
            Window = opened;
            application.MainWindow = opened;
            opened.Closed += (_, _) =>
            {
                Volatile.Write(ref windowClosed, 1);
                TryExit();
            };
        }

        internal void MarkJobFinished()
        {
            Volatile.Write(ref jobDone, 1);
            if (Window == null) Volatile.Write(ref windowClosed, 1);
            TryExit();
        }

        void TryExit()
        {
            if (Volatile.Read(ref windowClosed) != 1 || Volatile.Read(ref jobDone) != 1) return;
            if (application.Dispatcher.CheckAccess()) Shutdown();
            else application.Dispatcher.BeginInvoke(Shutdown);
        }

        void Shutdown()
        {
            if (!application.Dispatcher.HasShutdownStarted) application.Shutdown();
        }

        static string NameOf(string path)
        {
            try { return Path.GetFileName(path); }
            catch (ArgumentException) { return ""; }
        }
    }

    sealed class MenuProgressGate
    {
        readonly Session session;
        MenuProgressModel? model;
        MenuProgressWindow? window;
        int settled;
        int progressClosed;

        internal MenuProgressGate(Session session) { this.session = session; }

        internal void Notice(MenuConversion.Request request, string message)
        {
            if (Volatile.Read(ref settled) != 0) return;
            void Apply()
            {
                if (Volatile.Read(ref settled) != 0) return;
                switch (MenuProgressRouter.Classify(message, model != null, out string format))
                {
                    case MenuProgressRouter.Kind.Busy:
                        Volatile.Write(ref settled, 1);
                        Show(MenuProgressModel.Reject(NameOf(request.File), MenuProgressRouter.DisplayFormat(request.Format), message, busy: true));
                        break;
                    case MenuProgressRouter.Kind.Started:
                        model = MenuProgressModel.Start(NameOf(request.File), format, session.Job);
                        session.Present(model);
                        window = session.Window;
                        break;
                    case MenuProgressRouter.Kind.Validation:
                        Volatile.Write(ref settled, 1);
                        Show(MenuProgressModel.Reject(NameOf(request.File), MenuProgressRouter.DisplayFormat(request.Format), message, busy: false));
                        break;
                }
            }
            Dispatch(Apply);
        }

        internal void Report(int percent)
        {
            if (Volatile.Read(ref progressClosed) != 0) return;
            Dispatch(() =>
            {
                if (Volatile.Read(ref progressClosed) != 0 || model == null || window == null) return;
                if (!model.Report(percent)) return;
                window.Refresh();
            });
        }

        internal void Complete(MenuConversion.Outcome outcome)
        {
            if (Interlocked.Exchange(ref settled, 1) != 0) return;
            Volatile.Write(ref progressClosed, 1);
            Dispatch(() =>
            {
                if (model == null) Show(MenuProgressModel.FromOutcome(outcome));
                else
                {
                    model.Complete(outcome);
                    window?.Refresh();
                }
            });
        }

        void Show(MenuProgressModel next)
        {
            model = next;
            session.Present(next);
            window = session.Window;
        }

        void Dispatch(Action action)
        {
            var dispatcher = window?.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.BeginInvoke(action);
        }

        static string NameOf(string path)
        {
            try { return Path.GetFileName(path); }
            catch (ArgumentException) { return ""; }
        }
    }
}
