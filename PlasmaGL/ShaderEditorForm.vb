Imports System.Drawing
Imports System.Windows.Forms
Imports OpenTK
Imports OpenTK.Graphics
Imports OpenTK.Graphics.OpenGL
Imports OpenTK.WinForms

''' <summary>
''' Modal dialog for authoring / editing a GLSL fragment shader with a live preview.
''' When <paramref name="forceCopy"/> is True the name field is pre-set to a copy name
''' and the user cannot overwrite the original (used for built-in shaders).
''' </summary>
Public Class ShaderEditorForm : Inherits Form

    ' ── state ───────────────────────────────────────────────────
    Private ReadOnly _forceCopy As Boolean    ' True = user MUST save under a new name
    Private _lastGoodProgram As Integer = 0   ' last successfully compiled GL program
    Private _glReady As Boolean = False
    Private _phase As Single = 0.0F
    Private _seed1 As Single = 3.0F
    Private _seed2 As Single = 2.0F
    Private _scale As Single = 30.0F

    ' ── GL objects (owned by preview) ───────────────────────────
    Private _gl As GLControl
    Private _vao, _vbo As Integer
    Private ReadOnly _renderTimer As New Timer With {.Interval = 16}
    Private ReadOnly _debounce As New Timer With {.Interval = 1500}

    ' ── controls ────────────────────────────────────────────────
    Private _txtName As TextBox
    Private _rtbCode As RichTextBox
    Private _lblError As Label
    Private _btnSave As Button
    Private _btnCompile As Button

    ''' <summary>Set on successful save; caller reads this to refresh the shader list.</summary>
    Public Property SavedEntry As ShaderEntry = Nothing

    ' ============================================================
    '  CONSTRUCTOR
    ' ============================================================
    Public Sub New(entry As ShaderEntry, Optional forceCopy As Boolean = False)
        _forceCopy = forceCopy OrElse entry.IsBuiltIn
        BuildUI(entry)
    End Sub

    ' ============================================================
    '  UI CONSTRUCTION
    ' ============================================================
    Private Sub BuildUI(entry As ShaderEntry)
        Text = If(_forceCopy, "Shader Editor  –  Fork of """ & entry.Name & """",
                              "Shader Editor  –  " & entry.Name)
        Size = New Size(1000, 640)
        MinimumSize = New Size(800, 500)
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.FromArgb(30, 30, 30)
        ForeColor = Color.FromArgb(220, 220, 220)
        Font = New Font("Segoe UI", 9)

        ' ── top strip: name ─────────────────────────────────────
        Dim namePanel As New Panel With {
            .Dock = DockStyle.Top, .Height = 36, .Padding = New Padding(6, 4, 6, 4),
            .BackColor = Color.FromArgb(45, 45, 45)
        }
        Dim lblName As New Label With {
            .Text = "Shader name:", .AutoSize = True,
            .ForeColor = Color.FromArgb(180, 180, 180),
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(8, 9)
        }
        _txtName = New TextBox With {
            .Text = If(_forceCopy, entry.Name & " (copy)", entry.Name),
            .Left = 100, .Top = 5, .Width = 400, .Height = 24,
            .BackColor = Color.FromArgb(60, 60, 60),
            .ForeColor = Color.FromArgb(220, 220, 220),
            .BorderStyle = BorderStyle.FixedSingle,
            .Font = New Font("Segoe UI", 9)
        }
        namePanel.Controls.AddRange({lblName, _txtName})

        ' ── error strip ─────────────────────────────────────────
        _lblError = New Label With {
            .Dock = DockStyle.Bottom, .Height = 60, .AutoSize = False,
            .ForeColor = Color.FromArgb(255, 100, 80),
            .BackColor = Color.FromArgb(50, 20, 20),
            .Font = New Font("Courier New", 8),
            .Padding = New Padding(6),
            .TextAlign = ContentAlignment.TopLeft,
            .Visible = False
        }

        ' ── button strip ────────────────────────────────────────
        Dim btnPanel As New Panel With {
            .Dock = DockStyle.Bottom, .Height = 42,
            .BackColor = Color.FromArgb(45, 45, 45)
        }
        _btnSave = New Button With {
            .Text = "Save", .Width = 90, .Height = 28, .Top = 7, .Left = 8,
            .BackColor = Color.FromArgb(0, 122, 204),
            .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat,
            .Enabled = False
        }
        _btnSave.FlatAppearance.BorderSize = 0
        Dim btnCancel As New Button With {
            .Text = "Cancel", .Width = 90, .Height = 28, .Top = 7, .Left = 106,
            .BackColor = Color.FromArgb(80, 80, 80),
            .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat,
            .DialogResult = DialogResult.Cancel
        }
        btnCancel.FlatAppearance.BorderSize = 0
        btnPanel.Controls.AddRange({_btnSave, btnCancel})

        ' ── main split ──────────────────────────────────────────
        ' IMPORTANT: create with NO size constraints, add to Controls first so the
        ' control gets the form's real client width, THEN apply min sizes and splitter.
        Dim split As New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .BackColor = Color.FromArgb(30, 30, 30)
        }

        ' Left: code editor
        _rtbCode = New RichTextBox With {
            .Dock = DockStyle.Fill,
            .Text = entry.Source,
            .BackColor = Color.FromArgb(28, 28, 28),
            .ForeColor = Color.FromArgb(220, 220, 170),
            .Font = New Font("Courier New", 10),
            .WordWrap = False,
            .ScrollBars = RichTextBoxScrollBars.Both,
            .AcceptsTab = True,
            .BorderStyle = BorderStyle.None
        }
        split.Panel1.Controls.Add(_rtbCode)

        ' Right: preview panel + compile button
        Dim rightPanel As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(20, 20, 20)}

        _gl = New GLControl With {.Dock = DockStyle.Fill, .VSync = True}

        _btnCompile = New Button With {
            .Text = "▶  Compile && Preview", .Dock = DockStyle.Bottom, .Height = 32,
            .BackColor = Color.FromArgb(40, 140, 60),
            .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }
        _btnCompile.FlatAppearance.BorderSize = 0

        rightPanel.Controls.Add(_gl)
        rightPanel.Controls.Add(_btnCompile)
        split.Panel2.Controls.Add(rightPanel)

        ' ── assemble ─────────────────────────────────────────────
        ' Add split BEFORE setting min-size constraints (control must have real width first)
        Controls.Add(split)
        Controls.Add(_lblError)
        Controls.Add(btnPanel)
        Controls.Add(namePanel)

        ' Now safe to set constraints – form layout has run and split has real width
        split.Panel1MinSize = 300
        split.Panel2MinSize = 240

        ' ── events ──────────────────────────────────────────────
        AddHandler _gl.Load, AddressOf GL_Load
        AddHandler _gl.Paint, AddressOf GL_Paint
        AddHandler _gl.Resize, AddressOf GL_Resize
        AddHandler _btnCompile.Click, AddressOf BtnCompile_Click
        AddHandler _btnSave.Click, AddressOf BtnSave_Click
        AddHandler _rtbCode.TextChanged, AddressOf Code_TextChanged
        AddHandler _debounce.Tick, AddressOf Debounce_Tick
        AddHandler _renderTimer.Tick, Sub()
            _phase += 0.025F
            If _glReady Then _gl.Invalidate()
        End Sub

        ' Set splitter position once the form is fully shown and sized
        AddHandler Me.Shown, Sub(s, ev)
            split.SplitterDistance = CInt(split.Width * 0.65)
        End Sub
    End Sub

    ' ============================================================
    '  GL SET-UP
    ' ============================================================
    Private Sub GL_Load(sender As Object, e As EventArgs)
        GL.ClearColor(Color.Black)

        Dim verts() As Single = {-1, -1, 0, 1,  1, -1, 0, 1,  -1, 1, 0, 1,  1, 1, 0, 1}
        _vao = GL.GenVertexArray()
        _vbo = GL.GenBuffer()
        GL.BindVertexArray(_vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.StaticDraw)
        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, False, 16, 0)
        GL.EnableVertexAttribArray(0)

        ' compile the initial shader
        Dim err As String = String.Empty
        _lastGoodProgram = TryCompile(_rtbCode.Text, err)
        ShowError(err)
        _glReady = True
        _btnSave.Enabled = (_lastGoodProgram <> 0)
        _renderTimer.Start()
    End Sub

    Private Sub GL_Paint(sender As Object, e As PaintEventArgs)
        If Not _glReady OrElse _lastGoodProgram = 0 Then Return
        GL.Clear(ClearBufferMask.ColorBufferBit)
        GL.UseProgram(_lastGoodProgram)
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "iTime"), _phase)
        GL.Uniform2(GL.GetUniformLocation(_lastGoodProgram, "iResolution"), CSng(_gl.Width), CSng(_gl.Height))
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "seed1"), _seed1)
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "seed2"), _seed2)
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "scale"), _scale)
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "phase"), _phase)
        GL.Uniform1(GL.GetUniformLocation(_lastGoodProgram, "palette"), 0)
        GL.BindVertexArray(_vao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        _gl.SwapBuffers()
    End Sub

    Private Sub GL_Resize(sender As Object, e As EventArgs)
        If _glReady Then GL.Viewport(0, 0, _gl.Width, _gl.Height)
    End Sub

    ' ============================================================
    '  COMPILE LOGIC
    ' ============================================================
    Private Function TryCompile(fragSrc As String, ByRef errorMsg As String) As Integer
        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, ShaderLibrary.VertexSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, fragSrc)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)

        If vsOk = 0 OrElse fsOk = 0 Then
            errorMsg = (GL.GetShaderInfoLog(vs) & vbLf & GL.GetShaderInfoLog(fs)).Trim()
            GL.DeleteShader(vs) : GL.DeleteShader(fs)
            Return 0
        End If

        Dim prog = GL.CreateProgram()
        GL.AttachShader(prog, vs) : GL.AttachShader(prog, fs)
        GL.LinkProgram(prog)
        GL.DetachShader(prog, vs) : GL.DeleteShader(vs)
        GL.DetachShader(prog, fs) : GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(prog, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            errorMsg = GL.GetProgramInfoLog(prog)
            GL.DeleteProgram(prog)
            Return 0
        End If

        errorMsg = String.Empty
        Return prog
    End Function

    Private Sub ShowError(msg As String)
        If String.IsNullOrWhiteSpace(msg) Then
            _lblError.Visible = False
        Else
            _lblError.Text = "⚠  " & msg.Replace(vbCr, "").Replace(vbLf, "  │  ").TrimEnd(" │".ToCharArray())
            _lblError.Visible = True
        End If
    End Sub

    ' ============================================================
    '  EVENT HANDLERS
    ' ============================================================
    Private Sub BtnCompile_Click(sender As Object, e As EventArgs)
        If Not _glReady Then Return
        _debounce.Stop()
        CompileAndApply()
    End Sub

    Private Sub CompileAndApply()
        Dim err As String = String.Empty
        Dim newProg = TryCompile(_rtbCode.Text, err)
        ShowError(err)
        If newProg <> 0 Then
            If _lastGoodProgram <> 0 Then GL.DeleteProgram(_lastGoodProgram)
            _lastGoodProgram = newProg
            _btnSave.Enabled = True
        Else
            _btnSave.Enabled = False
        End If
    End Sub

    Private Sub Code_TextChanged(sender As Object, e As EventArgs)
        _btnSave.Enabled = False
        _debounce.Stop()
        _debounce.Start()
    End Sub

    Private Sub Debounce_Tick(sender As Object, e As EventArgs)
        _debounce.Stop()
        If _glReady Then CompileAndApply()
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        Dim name = _txtName.Text.Trim()
        If String.IsNullOrEmpty(name) Then
            MessageBox.Show("Please enter a shader name.", "PlasmaGL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ' Prevent saving over a built-in name
        If ShaderLibrary.IsBuiltIn(name) Then
            MessageBox.Show("That name is reserved for a built-in shader. Please choose a different name.",
                            "PlasmaGL", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ShaderLibrary.SaveUserShader(name, _rtbCode.Text)
        SavedEntry = New ShaderEntry With {.Name = name, .Source = _rtbCode.Text, .IsBuiltIn = False}
        DialogResult = DialogResult.OK
        Close()
    End Sub

    ' ============================================================
    '  CLEANUP
    ' ============================================================
    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _renderTimer.Stop()
        _debounce.Stop()
        If _glReady Then
            If _lastGoodProgram <> 0 Then GL.DeleteProgram(_lastGoodProgram)
            GL.DeleteBuffer(_vbo)
            GL.DeleteVertexArray(_vao)
        End If
        MyBase.OnFormClosed(e)
    End Sub

End Class
