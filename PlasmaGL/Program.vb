Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Module NativeMethods
    <Runtime.InteropServices.DllImport("user32.dll")>
    Public Function SetParent(hWndChild As IntPtr, hWndNewParent As IntPtr) As IntPtr
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Public Function GetClientRect(hWnd As IntPtr, ByRef lpRect As RECT) As Boolean
    End Function

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Public Structure RECT
        Public left, top, right, bottom As Integer
    End Structure
End Module
Friend Module SaverState
    ' remember first position on the screen that started the saver
    Friend MouseStart As Point
    ' total movement allowed before we bail out
    Friend Const DeltaThreshold As Integer = 5
End Module

Friend Module SeedChanger
    Private ReadOnly _timer As New Timers.Timer(60000) ' 60 s
    Private _forms As New List(Of PlasmaForm)

    Sub Start()
        AddHandler _timer.Elapsed, Sub() RefreshAll()
        _timer.Start()
    End Sub

    Sub StopTimer()
        _timer.Stop()
        _timer.Dispose()
    End Sub

    Sub Register(form As PlasmaForm)
        SyncLock _forms
            _forms.Add(form)
        End SyncLock
    End Sub

    Sub Unregister(form As PlasmaForm)
        SyncLock _forms
            _forms.Remove(form)
        End SyncLock
    End Sub

    Private Sub RefreshAll()
        SyncLock _forms
            For Each f In _forms.ToArray()
                f.NewRandomSeed()
            Next
        End SyncLock
    End Sub
End Module
Module Program
    <STAThread>
    Sub Main(args As String())
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Dim cmd As String = If(args.Length > 0, args(0).ToLower(), String.Empty)

        '-----  SHOW SETTINGS  -----
        If cmd.StartsWith("/c") Then
            Using d As New SettingsForm()
                d.ShowDialog()
            End Using
            Return
        End If

        '-----  PREVIEW (pass the supplied HWND)  -----
        If cmd.StartsWith("/p") Then
            ' arg(1) = parent window handle
            If args.Length >= 2 Then
                Dim parent As New IntPtr(Long.Parse(args(1)))
                Dim host As New Form With {
                    .FormBorderStyle = FormBorderStyle.None,
                    .TopMost = True,
                    .ShowInTaskbar = False,
                    .StartPosition = FormStartPosition.Manual,
                    .Location = Drawing.Point.Empty
                }
                Dim preview As New PlasmaPreview With {.Dock = DockStyle.Fill}
                host.Controls.Add(preview)
                NativeMethods.SetParent(host.Handle, parent)   ' embed inside preview pane
                
                Dim rect As NativeMethods.RECT
                NativeMethods.GetClientRect(parent, rect)
                host.Size = New Drawing.Size(rect.right - rect.left, rect.bottom - rect.top)
                
                Application.Run(host)                          ' Run on the *form*
            End If
            Return
        End If

        '-----  REAL SAVER (full-screen on all monitors)  -----
        If cmd.StartsWith("/s") Then
            SaverState.MouseStart = Cursor.Position

            SeedChanger.Start()          ' <— start 60-second changer

            Dim screens As New List(Of PlasmaForm)
            For Each scr In Screen.AllScreens
                Dim f As New PlasmaForm With {.Bounds = scr.Bounds}
                SeedChanger.Register(f)  ' register each form
                screens.Add(f)
            Next

            For Each f In screens
                AddHandler f.FormClosed,
            Sub()
                SeedChanger.Unregister(f)
                If Application.OpenForms.Count = 0 Then
                    SeedChanger.StopTimer()
                    Application.Exit()
                End If
            End Sub
            Next

            screens.ForEach(Sub(f) f.Show())
            Application.Run()
            Return
        End If

        '-----  NO SWITCH → open settings (handy for F5 debug)  -----
        Using d As New SettingsForm()
            d.ShowDialog()
        End Using
    End Sub
End Module