Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.Win32
Imports OpenTK
Imports OpenTK.Graphics
Imports OpenTK.Graphics.OpenGL
Imports OpenTK.WinForms

Public Class PlasmaForm : Inherits Form
    ' --------------------  fields  --------------------
    Private ReadOnly _gl As GLControl          ' the WinForms control
    Private _shader As Integer                   ' compiled program
    Private _vao, _vbo As Integer                ' vertex array / buffer
    Private _locTime, _locRes, _locSeed1, _locSeed2, _locScale, _locPhase As Integer
    Dim ran As New Random()
    Private _speed As Integer = 16        ' ms / frame  (60 FPS default)
    Private _palette As Integer = 0         ' 0=RGB 1=G 2=R 3=B
    Private _scale As Single = ran.Next(2, 60)         ' plasma blob scale
    Private _phase As Single = 0
    Private _seed1 As Single = ran.Next(1, 6)
    Private _seed2 As Single = ran.Next(1, 6)
    Private _shaderName As String = "Plasma (default)"

    ' --------------------  constructor  --------------------
    Public Sub New()

        Text = "PlasmaGL – OpenTK 3.3.3"
        WindowState = FormWindowState.Maximized
        FormBorderStyle = FormBorderStyle.None
        TopMost = True
        DoubleBuffered = True
        BackColor = Color.Black
        Cursor.Hide()

        NewRandomSeed()
        LoadSettings()
        ' create GLControl and fill window
        _gl = New GLControl With {.Dock = DockStyle.Fill, .VSync = True}
        Controls.Add(_gl)

        ' wire events
        AddHandler _gl.Load, AddressOf GL_Load
        AddHandler _gl.Paint, AddressOf GL_Paint
        AddHandler _gl.Resize, AddressOf GL_Resize
        AddHandler KeyDown, AddressOf Form_KeyDown
        AddHandler MouseMove, AddressOf Form_MouseMove
        AddHandler _gl.MouseMove, AddressOf GLControl_MouseMove
        AddHandler SettingsForm.SettingsChanged, AddressOf ApplySettings

    End Sub

    ' ============================================================
    '                       OPENGL  SET-UP
    ' ============================================================
    Private Sub GL_Load(sender As Object, e As EventArgs)
        GL.ClearColor(Color.Black)

        ' full-screen quad  (triangle-strip: 4 verts)
        Dim verts() As Single = {
            -1.0F, -1.0F, 0.0F, 1.0F,
             1.0F, -1.0F, 0.0F, 1.0F,
            -1.0F, 1.0F, 0.0F, 1.0F,
             1.0F, 1.0F, 0.0F, 1.0F
        }

        ' generate and fill VAO / VBO
        _vao = GL.GenVertexArray()
        _vbo = GL.GenBuffer()

        GL.BindVertexArray(_vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.StaticDraw)

        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, False, 16, 0)
        GL.EnableVertexAttribArray(0)

        ' compile shaders from library
        _shader = CreateShader(ShaderLibrary.VertexSource, ShaderLibrary.GetShaderSource(_shaderName))
        QueryUniformLocations()

        ' start 60 FPS timer
        AddHandler _timer.Tick, Sub()
                                    _phase += 0.025F        ' colour-cycle speed
                                    _gl.Invalidate()
                                End Sub
        _timer.Interval = _speed
        _timer.Start()
    End Sub

    ' ============================================================
    '                       RENDER  LOOP
    ' ============================================================
    Private ReadOnly _timer As New Timer With {.Interval = 16}

    Private Sub GL_Paint(sender As Object, e As PaintEventArgs)

        GL.Clear(ClearBufferMask.ColorBufferBit)
        GL.UseProgram(_shader)

        ' push uniforms
        Dim locPal As Integer = GL.GetUniformLocation(_shader, "palette")
        GL.Uniform1(locPal, _palette)
        GL.Uniform1(_locTime, _phase)
        GL.Uniform2(_locRes, CSng(_gl.Width), CSng(_gl.Height))
        GL.Uniform1(_locSeed1, _seed1)
        GL.Uniform1(_locSeed2, _seed2)
        GL.Uniform1(_locScale, _scale)
        GL.Uniform1(_locPhase, _phase)

        ' draw full-screen quad
        GL.BindVertexArray(_vao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)

        _gl.SwapBuffers()
    End Sub

    Private Sub GL_Resize(sender As Object, e As EventArgs)
        GL.Viewport(0, 0, _gl.Width, _gl.Height)
    End Sub

    ' ============================================================
    '                       HELPER FUNCTIONS
    ' ============================================================

    ' called by preview – compile shaders only once
    Public Sub CreateShaders()
        If _shader <> 0 Then Return          ' already done
        _shader = CreateShader(ShaderLibrary.VertexSource, ShaderLibrary.GetShaderSource(_shaderName))
        QueryUniformLocations()
    End Sub

    ' re-query all uniform locations after a recompile
    Private Sub QueryUniformLocations()
        _locTime  = GL.GetUniformLocation(_shader, "iTime")
        _locRes   = GL.GetUniformLocation(_shader, "iResolution")
        _locSeed1 = GL.GetUniformLocation(_shader, "seed1")
        _locSeed2 = GL.GetUniformLocation(_shader, "seed2")
        _locScale = GL.GetUniformLocation(_shader, "scale")
        _locPhase = GL.GetUniformLocation(_shader, "phase")
    End Sub

    ' called by preview – set up VAO/VBO for any size
    Public Sub InitialiseResources(width As Integer, height As Integer)
        Dim verts() As Single = {
            -1, -1, 0, 1, 1, -1, 0, 1,
            -1, 1, 0, 1, 1, 1, 0, 1
        }
        _vao = GL.GenVertexArray()
        _vbo = GL.GenBuffer()
        GL.BindVertexArray(_vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.StaticDraw)
        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, False, 16, 0)
        GL.EnableVertexAttribArray(0)
        GL.Viewport(0, 0, width, height)
    End Sub

    ' one-frame draw (used by preview) – pass target control so we can SwapBuffers
    Public Sub RenderOneFrame(target As GLControl, phase As Single)
        Dim locPal = GL.GetUniformLocation(_shader, "palette")
        GL.Uniform1(locPal, _palette)
        GL.Clear(ClearBufferMask.ColorBufferBit)
        GL.UseProgram(_shader)
        GL.Uniform1(_locTime, phase)
        GL.Uniform2(_locRes, CSng(target.Width), CSng(target.Height))
        GL.Uniform1(_locSeed1, _seed1)
        GL.Uniform1(_locSeed2, _seed2)
        GL.Uniform1(_locScale, _scale)
        GL.Uniform1(_locPhase, phase)
        GL.BindVertexArray(_vao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        target.SwapBuffers()
    End Sub

    ' ============================================================
    '                       EXIT  HANDLERS
    ' ============================================================
    Private Sub Form_KeyDown(sender As Object, e As KeyEventArgs)
        Close()
    End Sub

    Private Sub InitializeComponent()
        Me.SuspendLayout()
        Me.ClientSize = New System.Drawing.Size(284, 261)
        Me.Name = "PlasmaForm"
        Me.ResumeLayout(False)
    End Sub

    Private Sub LoadSettings()
        Using key = Registry.CurrentUser.CreateSubKey("Software\PlasmaGL")
            _speed      = CInt(key.GetValue("Speed",      16))
            _palette    = CInt(key.GetValue("Palette",    0))
            _scale      = CSng(key.GetValue("Scale",      ran.Next(2, 60)))
            _shaderName = CStr(key.GetValue("ShaderName", "Plasma (default)"))
        End Using
    End Sub

    Public Sub ApplySettings()
        Dim prevShader = _shaderName
        LoadSettings()
        _timer.Interval = _speed
        ' Recompile if the selected shader changed
        If _shaderName <> prevShader Then
            GL.UseProgram(0)
            GL.DeleteProgram(_shader)
            _shader = CreateShader(ShaderLibrary.VertexSource, ShaderLibrary.GetShaderSource(_shaderName))
            QueryUniformLocations()
        End If
    End Sub

    Public Sub NewRandomSeed()
        _seed1 = ran.Next(1, 6)
        _seed2 = ran.Next(1, 6)
        _scale = ran.Next(2, 60)
    End Sub

    Private lastPos As Point
    Private Sub Form_MouseMove(sender As Object, e As MouseEventArgs) Handles MyBase.MouseMove
        If lastPos <> Point.Empty AndAlso
           (Math.Abs(e.X - lastPos.X) > 5 OrElse Math.Abs(e.Y - lastPos.Y) > 5) Then
            Close()
        End If
        lastPos = e.Location
    End Sub

    Private Sub PlasmaForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
    End Sub

    Private Sub GLControl_MouseMove(sender As Object, e As MouseEventArgs)
        Static first As Boolean = True
        If first Then
            first = False : Return
        End If

        Dim total =
            Math.Abs(Cursor.Position.X - SaverState.MouseStart.X) +
            Math.Abs(Cursor.Position.Y - SaverState.MouseStart.Y)

        If total > SaverState.DeltaThreshold Then
            Close()
        End If
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        Cursor.Show()
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
        GL.UseProgram(0)
        GL.DeleteBuffer(_vbo)
        GL.DeleteVertexArray(_vao)
        GL.DeleteProgram(_shader)
        RemoveHandler SettingsForm.SettingsChanged, AddressOf ApplySettings
        MyBase.OnClosed(e)
    End Sub

    ' ============================================================
    '                       SHADER  UTIL
    ' ============================================================
    Private Function CreateShader(vsSrc As String, fsSrc As String) As Integer
        Dim vShader = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vShader, vsSrc)
        GL.CompileShader(vShader)

        Dim fShader = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fShader, fsSrc)
        GL.CompileShader(fShader)

        Dim prog = GL.CreateProgram()
        GL.AttachShader(prog, vShader)
        GL.AttachShader(prog, fShader)
        GL.LinkProgram(prog)

        GL.DetachShader(prog, vShader) : GL.DeleteShader(vShader)
        GL.DetachShader(prog, fShader) : GL.DeleteShader(fShader)
        Return prog
    End Function

    Private Sub PlasmaForm_MouseClick(sender As Object, e As MouseEventArgs) Handles Me.MouseClick
        Close()
    End Sub

    ' GLSL sources are in ShaderLibrary.vb.

End Class