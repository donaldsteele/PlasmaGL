Imports Microsoft.Win32
Imports System.Drawing
Imports System.Windows.Forms
Imports OpenTK
Imports OpenTK.Graphics.OpenGL
Imports OpenTK.WinForms

Public Class SettingsForm : Inherits Form

    ' ── shader section ──────────────────────────────────────────
    Private ReadOnly lstShaders As New ListBox With {.Height = 130, .IntegralHeight = False}
    Private ReadOnly btnNew As New Button With {.Text = "New…", .Width = 70}
    Private ReadOnly btnEdit As New Button With {.Text = "Edit…", .Width = 70}
    Private ReadOnly btnDelete As New Button With {.Text = "Delete", .Width = 70}

    ' ── existing controls ────────────────────────────────────────
    Private ReadOnly lblSpeed As New Label With {.Text = "Speed (ms per frame):", .AutoSize = True}
    Private ReadOnly trkSpeed As New TrackBar With {.Minimum = 10, .Maximum = 200, .TickFrequency = 10}
    Private ReadOnly lblPal As New Label With {.Text = "Colour palette:", .AutoSize = True}
    Private ReadOnly cmbPal As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private WithEvents btnOK As New Button With {.Text = "OK", .DialogResult = DialogResult.OK}
    Private ReadOnly btnCan As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel}

    ' ── preview section ─────────────────────────────────────────
    Private _glPreview As GLControl
    Private _previewForm As PlasmaForm          ' helper that owns shader / VAO / VBO
    Private ReadOnly _previewTimer As New Timer With {.Interval = 16}
    Private _previewPhase As Single
    Private _previewReady As Boolean = False

    Public Shared Event SettingsChanged()

    ' ============================================================
    '  CONSTRUCTOR
    ' ============================================================
    Public Sub New()
        Text = "PlasmaGL Settings"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False : MinimizeBox = False
        StartPosition = FormStartPosition.CenterScreen
        AcceptButton = btnOK : CancelButton = btnCan
        ClientSize = New Size(340, 680)
        BackColor = Color.FromArgb(45, 45, 48)
        ForeColor = Color.FromArgb(220, 220, 220)
        Font = New Font("Segoe UI", 9)

        cmbPal.Items.AddRange({
            "RGB cycle (acid)", "Green only", "Red only", "Blue only",
            "Yellow-Orange-Red", "Cool vivid", "Sunset", "Tropical",
            "Cyberpunk", "Neon Ice", "Galaxy"
        })

        BuildLayout()
        WireEvents()
        RefreshShaderList()
        LoadSettings()
    End Sub

    ' ============================================================
    '  LAYOUT
    ' ============================================================
    Private Sub BuildLayout()
        Dim pad = New Padding(10, 6, 10, 4)

        ' ── shader group ────────────────────────────────────────
        Dim grpShader As New GroupBox With {
            .Text = "Shader", .ForeColor = Color.FromArgb(180, 180, 180),
            .Left = 10, .Top = 10, .Width = 318, .Height = 200
        }

        lstShaders.Left = 8 : lstShaders.Top = 20
        lstShaders.Width = 298 : lstShaders.Height = 120
        lstShaders.BackColor = Color.FromArgb(30, 30, 30)
        lstShaders.ForeColor = Color.FromArgb(220, 220, 220)
        lstShaders.BorderStyle = BorderStyle.FixedSingle

        Dim btnY = 148
        StyleButton(btnNew,    Color.FromArgb(0, 122, 204)) : btnNew.Left = 8   : btnNew.Top = btnY
        StyleButton(btnEdit,   Color.FromArgb(80, 80, 80))  : btnEdit.Left = 84  : btnEdit.Top = btnY
        StyleButton(btnDelete, Color.FromArgb(160, 40, 40)) : btnDelete.Left = 160 : btnDelete.Top = btnY
        btnDelete.Width = 80

        grpShader.Controls.AddRange({lstShaders, btnNew, btnEdit, btnDelete})

        ' ── separator ───────────────────────────────────────────
        Dim sep As New Panel With {
            .Left = 10, .Top = 218, .Width = 318, .Height = 1,
            .BackColor = Color.FromArgb(80, 80, 80)
        }

        ' ── speed ───────────────────────────────────────────────
        lblSpeed.Left = 14 : lblSpeed.Top = 228
        trkSpeed.Left = 10 : trkSpeed.Top = 246 : trkSpeed.Width = 316

        ' ── palette ─────────────────────────────────────────────
        lblPal.Left = 14 : lblPal.Top = 300
        cmbPal.Left = 14 : cmbPal.Top = 318 : cmbPal.Width = 298
        cmbPal.BackColor = Color.FromArgb(60, 60, 60)
        cmbPal.ForeColor = Color.FromArgb(220, 220, 220)

        ' ── separator 2 ────────────────────────────────────────
        Dim sep2 As New Panel With {
            .Left = 10, .Top = 352, .Width = 318, .Height = 1,
            .BackColor = Color.FromArgb(80, 80, 80)
        }

        ' ── preview group ───────────────────────────────────────
        Dim grpPreview As New GroupBox With {
            .Text = "Preview", .ForeColor = Color.FromArgb(180, 180, 180),
            .Left = 10, .Top = 360, .Width = 318, .Height = 260
        }

        _glPreview = New GLControl With {
            .Left = 8, .Top = 20,
            .Width = 298, .Height = 230,
            .VSync = False
        }
        _glPreview.BackColor = Color.Black

        grpPreview.Controls.Add(_glPreview)

        ' ── ok / cancel ─────────────────────────────────────────
        StyleButton(btnOK, Color.FromArgb(0, 122, 204))
        StyleButton(btnCan, Color.FromArgb(80, 80, 80))
        btnOK.Left = 144 : btnOK.Top = 638 : btnOK.Width = 88
        btnCan.Left = 240 : btnCan.Top = 638 : btnCan.Width = 88

        trkSpeed.BackColor = Color.FromArgb(45, 45, 48)

        Controls.AddRange({grpShader, sep, lblSpeed, trkSpeed, lblPal, cmbPal,
                           sep2, grpPreview, btnOK, btnCan})
    End Sub

    Private Shared Sub StyleButton(btn As Button, bg As Color)
        btn.BackColor = bg
        btn.ForeColor = Color.White
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 0
        btn.Height = 28
    End Sub

    ' ============================================================
    '  SHADER LIST
    ' ============================================================
    Private Sub RefreshShaderList()
        Dim selected = If(lstShaders.SelectedItem IsNot Nothing, lstShaders.SelectedItem.ToString(), String.Empty)
        lstShaders.Items.Clear()
        For Each s In ShaderLibrary.GetAllShaders()
            lstShaders.Items.Add(s)
        Next
        ' restore selection
        For i = 0 To lstShaders.Items.Count - 1
            Dim e = DirectCast(lstShaders.Items(i), ShaderEntry)
            If e.Name = selected Then lstShaders.SelectedIndex = i : Exit For
        Next
        If lstShaders.SelectedIndex < 0 AndAlso lstShaders.Items.Count > 0 Then
            lstShaders.SelectedIndex = 0
        End If
        UpdateShaderButtons()
    End Sub

    Private Function SelectedEntry() As ShaderEntry
        If lstShaders.SelectedItem Is Nothing Then Return Nothing
        Return DirectCast(lstShaders.SelectedItem, ShaderEntry)
    End Function

    Private Sub UpdateShaderButtons()
        Dim e = SelectedEntry()
        btnEdit.Enabled = (e IsNot Nothing)
        btnDelete.Enabled = (e IsNot Nothing AndAlso Not e.IsBuiltIn)
    End Sub

    ' ============================================================
    '  WIRE EVENTS
    ' ============================================================
    Private Sub WireEvents()
        AddHandler lstShaders.SelectedIndexChanged, Sub(s, e)
                                                        UpdateShaderButtons()
                                                        UpdatePreviewShader()
                                                    End Sub
        AddHandler lstShaders.DoubleClick, AddressOf OpenEditor

        AddHandler btnNew.Click, Sub(s, e)
            Dim blank As New ShaderEntry With {
                .Name = "My Shader", .Source = ShaderLibrary.BlankTemplate, .IsBuiltIn = False
            }
            Using dlg As New ShaderEditorForm(blank, forceCopy:=False)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.SavedEntry IsNot Nothing Then
                    RefreshShaderList()
                    SelectShaderByName(dlg.SavedEntry.Name)
                End If
            End Using
        End Sub

        AddHandler btnEdit.Click, AddressOf OpenEditor

        AddHandler btnDelete.Click, Sub(s, e)
            Dim entry = SelectedEntry()
            If entry Is Nothing OrElse entry.IsBuiltIn Then Return
            If MessageBox.Show("Delete shader """ & entry.Name & """?",
                               "PlasmaGL", MessageBoxButtons.YesNo,
                               MessageBoxIcon.Warning) = DialogResult.Yes Then
                ShaderLibrary.DeleteUserShader(entry.Name)
                RefreshShaderList()
            End If
        End Sub

        ' palette change → update preview palette
        AddHandler cmbPal.SelectedIndexChanged, Sub(s, e)
                                                    UpdatePreviewPalette()
                                                End Sub

        ' preview GL setup
        AddHandler _glPreview.Load, AddressOf PreviewGL_Load

        ' preview animation timer
        AddHandler _previewTimer.Tick, Sub(s, e)
                                           If Not _previewReady Then Return
                                           _previewPhase += 0.025F
                                           Try
                                               _previewForm.RenderOneFrame(_glPreview, _previewPhase)
                                           Catch
                                               ' Swallow GL errors during shader transitions
                                           End Try
                                       End Sub
    End Sub

    Private Sub OpenEditor(sender As Object, e As EventArgs)
        Dim entry = SelectedEntry()
        If entry Is Nothing Then Return
        ' Built-ins open in fork (copy) mode; user shaders open normally
        Using dlg As New ShaderEditorForm(entry, forceCopy:=entry.IsBuiltIn)
            If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.SavedEntry IsNot Nothing Then
                RefreshShaderList()
                SelectShaderByName(dlg.SavedEntry.Name)
            End If
        End Using
    End Sub

    Private Sub SelectShaderByName(name As String)
        For i = 0 To lstShaders.Items.Count - 1
            If DirectCast(lstShaders.Items(i), ShaderEntry).Name = name Then
                lstShaders.SelectedIndex = i : Return
            End If
        Next
    End Sub

    ' ============================================================
    '  PREVIEW – GL SETUP & UPDATES
    ' ============================================================
    Private Sub PreviewGL_Load(sender As Object, e As EventArgs)
        Try
            _previewForm = New PlasmaForm()
            Cursor.Show()   ' PlasmaForm constructor hides the cursor; restore it

            ' Determine which shader to start with
            Dim entry = SelectedEntry()
            Dim shaderName = If(entry IsNot Nothing, entry.Name, "Plasma (default)")

            _previewForm.CreateShaders()
            _previewForm.InitialiseResources(_glPreview.ClientSize.Width, _glPreview.ClientSize.Height)

            ' Apply the currently selected shader + palette
            _previewForm.SetShader(shaderName)
            If cmbPal.SelectedIndex >= 0 Then
                _previewForm.SetPalette(cmbPal.SelectedIndex)
            End If

            _previewReady = True
            _previewTimer.Start()
        Catch ex As Exception
            ' If GL init fails, just leave the preview black
            _previewReady = False
        End Try
    End Sub

    ''' <summary>Called when the shader listbox selection changes.</summary>
    Private Sub UpdatePreviewShader()
        If Not _previewReady Then Return
        Dim entry = SelectedEntry()
        If entry Is Nothing Then Return
        Try
            _glPreview.MakeCurrent()
            _previewForm.SetShader(entry.Name)
        Catch
            ' Swallow compile errors for invalid user shaders
        End Try
    End Sub

    ''' <summary>Called when the palette combobox selection changes.</summary>
    Private Sub UpdatePreviewPalette()
        If Not _previewReady Then Return
        If cmbPal.SelectedIndex < 0 Then Return
        _previewForm.SetPalette(cmbPal.SelectedIndex)
    End Sub

    ' ============================================================
    '  LOAD / SAVE REGISTRY
    ' ============================================================
    Private Sub LoadSettings()
        Using key = Registry.CurrentUser.CreateSubKey("Software\PlasmaGL")
            trkSpeed.Value = CInt(key.GetValue("Speed", 16))
            cmbPal.SelectedIndex = CInt(key.GetValue("Palette", 0))
            Dim sn = CStr(key.GetValue("ShaderName", "Plasma (default)"))
            SelectShaderByName(sn)
        End Using
    End Sub

    Private Sub SaveSettings()
        Using key = Registry.CurrentUser.CreateSubKey("Software\PlasmaGL")
            key.SetValue("Speed", trkSpeed.Value, RegistryValueKind.DWord)
            key.SetValue("Palette", cmbPal.SelectedIndex, RegistryValueKind.DWord)
            Dim entry = SelectedEntry()
            key.SetValue("ShaderName",
                         If(entry IsNot Nothing, entry.Name, "Plasma (default)"),
                         RegistryValueKind.String)
        End Using
    End Sub

    ' ============================================================
    '  OK BUTTON
    ' ============================================================
    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        SaveSettings()
        RaiseEvent SettingsChanged()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    ' ============================================================
    '  CLEANUP
    ' ============================================================
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _previewTimer.Stop()
        _previewReady = False
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub InitializeComponent()
        Me.SuspendLayout()
        Me.Name = "SettingsForm"
        Me.ResumeLayout(False)
    End Sub

End Class