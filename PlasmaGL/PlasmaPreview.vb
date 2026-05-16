Imports System.Windows.Forms
Imports OpenTK
Imports OpenTK.WinForms

''' <summary> Tiny viewport that runs inside the Display-Properties preview pane. </summary>
Public Class PlasmaPreview : Inherits GLControl
    Private ReadOnly _timer As New Timer With {.Interval = 16}
    Private _phase As Single

    Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
        _timer.Stop()
        MyBase.OnHandleDestroyed(e)
    End Sub

    Private Sub InitializeComponent()
        Me.SuspendLayout()
        '
        'PlasmaPreview
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.Name = "PlasmaPreview"
        Me.ResumeLayout(False)

    End Sub

    Private Sub PlasmaPreview_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim f As New PlasmaForm()
        f.CreateShaders()          ' public helper we’ll add in next step
        f.InitialiseResources(ClientSize.Width, ClientSize.Height)
        AddHandler _timer.Tick,
            Sub()
                _phase += 0.025F
                f.RenderOneFrame(Me, _phase)
            End Sub
        _timer.Start()
    End Sub
End Class