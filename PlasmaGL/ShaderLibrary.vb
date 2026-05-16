Imports System.IO
Imports System.Linq

''' <summary>A named GLSL fragment shader, either built-in (read-only) or user-authored.</summary>
Public Class ShaderEntry
    Public Property Name As String
    Public Property Source As String       ' fragment shader only; vertex is shared
    Public Property IsBuiltIn As Boolean
    Public Overrides Function ToString() As String
        Return If(IsBuiltIn, Name & "  [built-in]", Name)
    End Function
End Class

''' <summary>
''' Single source of truth for all shader sources and user-shader persistence.
''' User shaders live as *.frag files under %APPDATA%\PlasmaGL\shaders\.
''' </summary>
Friend Module ShaderLibrary

    Private ReadOnly _userDir As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "PlasmaGL", "shaders")

    ''' <summary>Display-ordered list of built-in shader names.</summary>
    Public ReadOnly BuiltInNames As String() = {
        "Plasma (default)", "Ripple", "Voronoi Cells", "Fire", "Wave Interference", "Fractal Pyramid"
    }

    ''' <summary>Shared vertex shader used by every shader in the library.</summary>
    Public ReadOnly VertexSource As String =
        "#version 330 core" & vbLf &
        "layout(location = 0) in vec4 pos;" & vbLf &
        "void main() { gl_Position = pos; }"

    ''' <summary>Blank template shown when the user clicks New.</summary>
    Public ReadOnly BlankTemplate As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "" & vbLf &
        "// Available uniforms (all optional):" & vbLf &
        "//   float iTime        - monotonic animation clock" & vbLf &
        "//   vec2  iResolution  - viewport size in pixels" & vbLf &
        "//   float seed1, seed2 - random seeds (1-5)" & vbLf &
        "//   float scale        - blob scale (2-60)" & vbLf &
        "//   float phase        - same as iTime, drives colour cycling" & vbLf &
        "//   int   palette      - user palette selection (0-7)" & vbLf &
        "" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv = gl_FragCoord.xy / iResolution.xy;" & vbLf &
        "    vec3 col = vec3(uv, 0.5 + 0.5 * sin(iTime));" & vbLf &
        "    fragColor = vec4(col, 1.0);" & vbLf &
        "}"

    ' ============================================================
    '  PUBLIC API
    ' ============================================================

    ''' <summary>Built-ins first (in order), then user shaders sorted alphabetically.</summary>
    Public Function GetAllShaders() As List(Of ShaderEntry)
        Dim list As New List(Of ShaderEntry)
        For Each n In BuiltInNames
            list.Add(New ShaderEntry With {.Name = n, .Source = GetShaderSource(n), .IsBuiltIn = True})
        Next
        EnsureUserDir()
        For Each f In Directory.GetFiles(_userDir, "*.frag").OrderBy(Function(x) Path.GetFileName(x))
            list.Add(New ShaderEntry With {
                .Name = Path.GetFileNameWithoutExtension(f),
                .Source = File.ReadAllText(f),
                .IsBuiltIn = False
            })
        Next
        Return list
    End Function

    ''' <summary>Returns source for the named shader; falls back to Plasma (default) if not found.</summary>
    Public Function GetShaderSource(name As String) As String
        If IsBuiltIn(name) Then Return GetBuiltInSource(name)
        Dim p = UserPath(name)
        If File.Exists(p) Then Return File.ReadAllText(p)
        Return GetBuiltInSource("Plasma (default)")
    End Function

    Public Function IsBuiltIn(name As String) As Boolean
        Return BuiltInNames.Contains(name, StringComparer.OrdinalIgnoreCase)
    End Function

    Public Sub SaveUserShader(name As String, source As String)
        EnsureUserDir()
        File.WriteAllText(UserPath(name), source)
    End Sub

    Public Sub DeleteUserShader(name As String)
        Dim p = UserPath(name)
        If File.Exists(p) Then File.Delete(p)
    End Sub

    Public Function UserShaderExists(name As String) As Boolean
        Return File.Exists(UserPath(name))
    End Function

    ' ============================================================
    '  PRIVATE HELPERS
    ' ============================================================

    Private Sub EnsureUserDir()
        If Not Directory.Exists(_userDir) Then Directory.CreateDirectory(_userDir)
    End Sub

    Private Function UserPath(name As String) As String
        Dim safe = String.Join("_", name.Split(Path.GetInvalidFileNameChars()))
        Return Path.Combine(_userDir, safe & ".frag")
    End Function

    Private Function GetBuiltInSource(name As String) As String
        Select Case name
            Case "Plasma (default)" : Return PlasmaFrag
            Case "Ripple" : Return RippleFrag
            Case "Voronoi Cells" : Return VoronoiFrag
            Case "Fire" : Return FireFrag
            Case "Wave Interference" : Return WaveInterferenceFrag
            Case "Fractal Pyramid" : Return FractalPyramidFrag
            Case Else : Return PlasmaFrag
        End Select
    End Function

    ' ============================================================
    '  BUILT-IN GLSL FRAGMENT SOURCES
    ' ============================================================

    Private ReadOnly PaletteGLSL As String =
        "vec3 getPalette(float p, float phase, int pal) {" & vbLf &
        "    if (pal == 0) return 0.5 + 0.5*cos(phase + p*6.28 + vec3(0,2.1,4.2));" & vbLf &
        "    if (pal == 2) return vec3(0.5+0.5*cos(phase+p*6.28318), 0.0, 0.0);" & vbLf &
        "    if (pal == 1) return vec3(0.0, 0.5+0.5*cos(phase+p*6.28318), 0.0);" & vbLf &
        "    if (pal == 4) {" & vbLf &
        "        float a=phase+p*6.28318; vec3 c = clamp(vec3(0.9+0.4*cos(a), 0.5+0.5*cos(a-1.05), 0.0),0.0,1.0);" & vbLf &
        "        return c*c;" & vbLf &
        "    }" & vbLf &
        "    if (pal == 5) {" & vbLf &
        "        float a=phase+p*6.28318; float s=a*2.0/3.14159; int i=int(floor(s))&3; float f=s-floor(s);" & vbLf &
        "        vec3 c0=vec3(0,1,1),c1=vec3(0,1,0),c2=vec3(0,0,1),c3=vec3(0.5,0,1);" & vbLf &
        "        vec3 now=i==0?c0:(i==1?c1:(i==2?c2:c3));" & vbLf &
        "        vec3 nxt=i==3?c0:(i==0?c1:(i==1?c2:c3));" & vbLf &
        "        vec3 c = mix(now,nxt,f); return c*c;" & vbLf &
        "    }" & vbLf &
        "    if (pal == 6) {" & vbLf &
        "        float a=phase+p*6.28318;" & vbLf &
        "        vec3 c1=vec3(0.20,0.00,0.60),c2=vec3(0.50,0.00,1.00),c3=vec3(1.00,0.20,0.60);" & vbLf &
        "        vec3 c4=vec3(1.00,0.50,0.00),c5=vec3(1.00,0.80,0.00);" & vbLf &
        "        float w1=0.5+0.5*cos(a),w2=0.5+0.5*cos(a-1.2566),w3=0.5+0.5*cos(a-2.5133);" & vbLf &
        "        float w4=0.5+0.5*cos(a-3.7699),w5=0.5+0.5*cos(a-5.0265);" & vbLf &
        "        vec3 c=clamp((w1*c1+w2*c2+w3*c3+w4*c4+w5*c5)/(w1+w2+w3+w4+w5),0.0,1.0);" & vbLf &
        "        return c*c;" & vbLf &
        "    }" & vbLf &
        "    if (pal == 7) {" & vbLf &
        "        float a=phase+p*6.28318; int sg=int(floor(a*4.0/6.28318))&3;" & vbLf &
        "        vec3 c0=vec3(0,1,1),c1=vec3(0,0.8,0.6),c2=vec3(0,0.9,0.3),c3=vec3(0.4,1,0);" & vbLf &
        "        vec3 c=(sg==0?c0:(sg==1?c1:(sg==2?c2:c3))); return c*c;" & vbLf &
        "    }" & vbLf &
        "    if (pal == 8) return mix(vec3(0.2,0.7,0.9), vec3(1.0,0.0,1.0), p);" & vbLf &
        "    return vec3(0.0, 0.0, 0.5+0.5*cos(phase+p*6.28318));" & vbLf &
        "}" & vbLf &
        "" & vbLf

    Private ReadOnly PlasmaFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "vec2 lens(vec2 uv, float t) {" & vbLf &
        "    float x = uv.x + 0.08*sin(uv.y*6.0+t*0.7) + 0.01*sin(uv.y*9.0-t*0.5);" & vbLf &
        "    float y = uv.y + 0.08*sin(uv.x*6.0+t*0.6) + 0.01*sin(uv.x*9.0-t*0.4);" & vbLf &
        "    return vec2(x,y);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float plasma(vec2 uv) {" & vbLf &
        "    float x = uv.x * iResolution.x;" & vbLf &
        "    float y = uv.y * iResolution.y;" & vbLf &
        "    float v = 0.0;" & vbLf &
        "    v += sin(x / (seed1 * scale));" & vbLf &
        "    v += sin(y / (seed2 * scale));" & vbLf &
        "    v += sin((x+y) / (scale*2.0));" & vbLf &
        "    v += sin(length(vec2(x,y)) / scale);" & vbLf &
        "    return (v + 4.0) / 8.0;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv = gl_FragCoord.xy / iResolution.xy;" & vbLf &
        "    uv = lens(uv, iTime);" & vbLf &
        "    float p = plasma(uv);" & vbLf &
        "    vec3 col = getPalette(p, phase, palette);" & vbLf &
        "    fragColor = vec4(col, 1.0);" & vbLf &
        "}"

    Private ReadOnly RippleFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "void main() {" & vbLf &
        "    vec2 uv = (gl_FragCoord.xy - iResolution*0.5) / min(iResolution.x,iResolution.y);" & vbLf &
        "    float r = length(uv);" & vbLf &
        "    float a = atan(uv.y, uv.x);" & vbLf &
        "    float sc = max(scale * 0.018, 0.08);" & vbLf &
        "    float v = sin(r/sc - phase*2.5)" & vbLf &
        "            + 0.6*sin(r/(sc*max(seed1*0.5,0.1)) - phase*1.7 + a*2.0)" & vbLf &
        "            + 0.3*sin(r/(sc*max(seed2*0.3,0.1)) + a*seed1    - phase*1.2);" & vbLf &
        "    float p = clamp(0.5 + 0.26*v, 0.0, 1.0);" & vbLf &
        "    vec3 col = getPalette(p, phase, palette);" & vbLf &
        "    fragColor = vec4(col, 1.0);" & vbLf &
        "}"

    Private ReadOnly VoronoiFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "vec2 hash2(vec2 p) {" & vbLf &
        "    p = vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3)));" & vbLf &
        "    return fract(sin(p)*43758.5453);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv  = gl_FragCoord.xy / iResolution.xy;" & vbLf &
        "    float sc = max(scale*0.12, 1.0);" & vbLf &
        "    vec2 st  = uv*(iResolution/min(iResolution.x,iResolution.y))*sc;" & vbLf &
        "    vec2 ist = floor(st);" & vbLf &
        "    vec2 fst = fract(st);" & vbLf &
        "    float minD=8.0, secD=8.0;" & vbLf &
        "    for(int y=-2;y<=2;y++) for(int x=-2;x<=2;x++) {" & vbLf &
        "        vec2 nb = vec2(float(x),float(y));" & vbLf &
        "        vec2 pt = 0.5+0.5*sin(phase*0.4+6.28318*hash2(ist+nb));" & vbLf &
        "        float d = length(nb+pt-fst);" & vbLf &
        "        if(d<minD){secD=minD;minD=d;} else if(d<secD){secD=d;}" & vbLf &
        "    }" & vbLf &
        "    float border = secD-minD;" & vbLf &
        "    vec3 col = getPalette((minD*5.0*seed1)/6.28318, phase, palette);" & vbLf &
        "    col = mix(col, vec3(1.0), smoothstep(0.0,0.06,border));" & vbLf &
        "    fragColor = vec4(col,1.0);" & vbLf &
        "}"

    Private ReadOnly FireFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}" & vbLf &
        "float noise(vec2 p){" & vbLf &
        "    vec2 i=floor(p); vec2 f=fract(p); f=f*f*(3.0-2.0*f);" & vbLf &
        "    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x)," & vbLf &
        "               mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);" & vbLf &
        "}" & vbLf &
        "float fbm(vec2 p){" & vbLf &
        "    float v=0.0,a=0.5;" & vbLf &
        "    for(int i=0;i<5;i++){v+=a*noise(p);p=p*2.1+vec2(seed1,seed2)*0.2;a*=0.5;}" & vbLf &
        "    return v;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main(){" & vbLf &
        "    vec2 uv = gl_FragCoord.xy/iResolution.xy;" & vbLf &
        "    float sc = max(scale*0.04,0.5);" & vbLf &
        "    vec2 st  = vec2((uv.x-0.5)*3.0*sc,(1.0-uv.y)*2.0*sc);" & vbLf &
        "    float n  = fbm(st+vec2(0.0,-phase*0.4))+0.4*fbm(st*2.2+vec2(0.3,-phase*0.6));" & vbLf &
        "    float strength = pow(clamp(1.0-uv.y,0.0,1.0),1.2)*1.8;" & vbLf &
        "    float fire = clamp(n*strength,0.0,1.0);" & vbLf &
        "    vec3 col = getPalette(fire, phase, palette);" & vbLf &
        "    fragColor = vec4(col,1.0);" & vbLf &
        "}"

    Private ReadOnly WaveInterferenceFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        "#define PI 3.14159" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "void main() {" & vbLf &
        "    vec2 uv = (gl_FragCoord.yx - 0.5*iResolution.yx) / min(iResolution.x, iResolution.y);" & vbLf &
        "    float time = 2.0 * PI * (iTime / 2.5);" & vbLf &
        "    float total = 0.0;" & vbLf &
        "    float zoom = 1.0 + 0.4 * sin(iTime * 0.1 * seed1) + 0.3 * cos(iTime * 0.15 * seed2);" & vbLf &
        "    float sc = max(scale * 4.0 * zoom, 10.0);" & vbLf &
        "    for (int i = 0; i < 7; i++) {" & vbLf &
        "        float angle = PI * float(i) / 7.0;" & vbLf &
        "        float len = dot(uv, vec2(cos(angle), sin(angle)));" & vbLf &
        "        total += (cos(2.0 * sc * len + time) + 1.0) / 2.0;" & vbLf &
        "    }" & vbLf &
        "    float p = 1.0 - abs(2.0 * fract(0.5 * total) - 1.0);" & vbLf &
        "    vec3 col = getPalette(p, phase, palette);" & vbLf &
        "    fragColor = vec4(col, 1.0);" & vbLf &
        "}"

    Private ReadOnly FractalPyramidFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "vec2 rotate(vec2 p, float a) {" & vbLf &
        "    float c = cos(a);" & vbLf &
        "    float s = sin(a);" & vbLf &
        "    return p * mat2(c, s, -s, c);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float map(vec3 p) {" & vbLf &
        "    for (int i = 0; i < 8; ++i) {" & vbLf &
        "        float t = iTime * 0.2;" & vbLf &
        "        p.xz = rotate(p.xz, t);" & vbLf &
        "        p.xy = rotate(p.xy, t * 1.89);" & vbLf &
        "        p.xz = abs(p.xz);" & vbLf &
        "        p.xz -= 0.5;" & vbLf &
        "    }" & vbLf &
        "    return dot(sign(p), p) / 5.0;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "vec4 rm(vec3 ro, vec3 rd) {" & vbLf &
        "    float t = 0.0;" & vbLf &
        "    vec3 col = vec3(0.0);" & vbLf &
        "    float d;" & vbLf &
        "    for (float i = 0.0; i < 64.0; i++) {" & vbLf &
        "        vec3 p = ro + rd * t;" & vbLf &
        "        d = map(p) * 0.5;" & vbLf &
        "        if (d < 0.02) break;" & vbLf &
        "        if (d > 100.0) break;" & vbLf &
        "        col += getPalette(length(p) * 0.1, phase, palette) / (400.0 * d);" & vbLf &
        "        t += d;" & vbLf &
        "    }" & vbLf &
        "    return vec4(col, 1.0 / (d * 100.0));" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv = (gl_FragCoord.xy - (iResolution.xy / 2.0)) / iResolution.x;" & vbLf &
        "    float z = -50.0 + (scale - 30.0) * 0.5;" & vbLf &
        "    vec3 ro = vec3(0.0, 0.0, z);" & vbLf &
        "    ro.xz = rotate(ro.xz, iTime);" & vbLf &
        "    vec3 cf = normalize(-ro);" & vbLf &
        "    vec3 cs = normalize(cross(cf, vec3(0.0, 1.0, 0.0)));" & vbLf &
        "    vec3 cu = normalize(cross(cf, cs));" & vbLf &
        "    vec3 uuv = ro + cf * 3.0 + uv.x * cs + uv.y * cu;" & vbLf &
        "    vec3 rd = normalize(uuv - ro);" & vbLf &
        "    vec4 col = rm(ro, rd);" & vbLf &
        "    fragColor = vec4(col.rgb, 1.0);" & vbLf &
        "}"

End Module
