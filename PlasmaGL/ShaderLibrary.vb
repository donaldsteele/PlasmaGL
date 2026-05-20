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
        "Plasma (default)", "Ripple", "Voronoi Cells", "Fire", "Wave Interference", "Fractal Pyramid", "Neon Fractal", "Starfield", "Fractal Galaxy", "Polka Dot"
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
            Case "Neon Fractal" : Return NeonFractalFrag
            Case "Starfield" : Return StarfieldFrag
            Case "Fractal Galaxy" : Return FractalGalaxyFrag
            Case "Polka Dot" : Return PolkaDotFrag
            Case Else : Return PlasmaFrag
        End Select
    End Function

    ' ============================================================
    '  BUILT-IN GLSL FRAGMENT SOURCES
    ' ============================================================

    Private ReadOnly PaletteGLSL As String =
        "vec3 getPalette(float p, float phase, int pal) {" & vbLf &
        "    if (pal == 0) return 0.5 + 0.5*cos(phase + p*6.28 + vec3(0,2.1,4.2));" & vbLf &
        "    if (pal == 2) return (0.5+0.5*cos(phase+p*6.28318)) * vec3(1.0, 0.05, 0.02);" & vbLf &
        "    if (pal == 1) return (0.5+0.5*cos(phase+p*6.28318)) * vec3(0.02, 1.0, 0.1);" & vbLf &
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
        "    if (pal == 9) return 0.5 + 0.5*cos(phase + p*6.28318 + vec3(1.652, 2.614, 3.500));" & vbLf &
        "    if (pal == 10) return vec3(p*p*p, p*p, p);" & vbLf &
        "    return (0.5+0.5*cos(phase+p*6.28318)) * vec3(0.02, 0.1, 1.0);" & vbLf &
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

    Private ReadOnly NeonFractalFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "void main() {" & vbLf &
        "    vec2 uv = (gl_FragCoord.xy * 2.0 - iResolution.xy) / iResolution.y;" & vbLf &
        "    vec2 uv0 = uv;" & vbLf &
        "    vec3 finalColor = vec3(0.0);" & vbLf &
        "    for (float i = 0.0; i < 4.0; i++) {" & vbLf &
        "        uv = fract(uv * 1.5) - 0.5;" & vbLf &
        "        float d = length(uv) * exp(-length(uv0));" & vbLf &
        "        vec3 col = getPalette(length(uv0) + i*0.4, iTime * 0.4 * 6.28318, palette);" & vbLf &
        "        d = sin(d*8.0 + iTime)/8.0;" & vbLf &
        "        d = abs(d);" & vbLf &
        "        d = pow(0.01 / d, 1.2);" & vbLf &
        "        finalColor += col * d;" & vbLf &
        "    }" & vbLf &
        "    fragColor = vec4(finalColor, 1.0);" & vbLf &
        "}"

    Private ReadOnly StarfieldFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "float field(in vec3 p) {" & vbLf &
        "    float strength = 7.0 + 0.03 * log(1.e-6 + fract(sin(iTime) * 4373.11));" & vbLf &
        "    float accum = 0.0;" & vbLf &
        "    float prev = 0.0;" & vbLf &
        "    float tw = 0.0;" & vbLf &
        "    for (int i = 0; i < 32; ++i) {" & vbLf &
        "        float mag = dot(p, p);" & vbLf &
        "        p = abs(p) / mag + vec3(-0.5, -0.4, -1.5);" & vbLf &
        "        float w = exp(-float(i) / 7.0);" & vbLf &
        "        accum += w * exp(-strength * pow(abs(mag - prev), 2.3));" & vbLf &
        "        tw += w;" & vbLf &
        "        prev = mag;" & vbLf &
        "    }" & vbLf &
        "    return max(0.0, 5.0 * accum / tw - 0.7);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv = 2.0 * gl_FragCoord.xy / iResolution.xy - 1.0;" & vbLf &
        "    vec2 uvs = uv * iResolution.xy / max(iResolution.x, iResolution.y);" & vbLf &
        "    vec3 p = vec3(uvs / 4.0, 0.0) + vec3(1.0, -1.3, 0.0);" & vbLf &
        "    p += 0.2 * vec3(sin(iTime / 16.0), sin(iTime / 12.0), sin(iTime / 128.0));" & vbLf &
        "    float t = field(p);" & vbLf &
        "    float v = (1.0 - exp((abs(uv.x) - 1.0) * 6.0)) * (1.0 - exp((abs(uv.y) - 1.0) * 6.0));" & vbLf &
        "    vec3 col = getPalette(t, phase, palette);" & vbLf &
        "    fragColor = vec4(col * mix(0.4, 1.0, v), 1.0);" & vbLf &
        "}"

    Private ReadOnly FractalGalaxyFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "float mod289(float x){return x - floor(x * (1.0 / 289.0)) * 289.0;}" & vbLf &
        "vec4 mod289(vec4 x){return x - floor(x * (1.0 / 289.0)) * 289.0;}" & vbLf &
        "vec4 perm(vec4 x){return mod289(((x * 34.0) + 1.0) * x);}" & vbLf &
        "" & vbLf &
        "float noise(vec3 p){" & vbLf &
        "    vec3 a = floor(p);" & vbLf &
        "    vec3 d = p - a;" & vbLf &
        "    d = d * d * (3.0 - 2.0 * d);" & vbLf &
        "    vec4 b = a.xxyy + vec4(0.0, 1.0, 0.0, 1.0);" & vbLf &
        "    vec4 k1 = perm(b.xyxy);" & vbLf &
        "    vec4 k2 = perm(k1.xyxy + b.zzww);" & vbLf &
        "    vec4 c = k2 + a.zzzz;" & vbLf &
        "    vec4 k3 = perm(c);" & vbLf &
        "    vec4 k4 = perm(c + 1.0);" & vbLf &
        "    vec4 o1 = fract(k3 * (1.0 / 41.0));" & vbLf &
        "    vec4 o2 = fract(k4 * (1.0 / 41.0));" & vbLf &
        "    vec4 o3 = o2 * d.z + o1 * (1.0 - d.z);" & vbLf &
        "    vec2 o4 = o3.yw * d.x + o3.xz * (1.0 - d.x);" & vbLf &
        "    return o4.y * d.y + o4.x * (1.0 - d.y);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float field(in vec3 p,float s) {" & vbLf &
        "    float strength = 7.0 + 0.03 * log(1.e-6 + fract(sin(iTime) * 4373.11));" & vbLf &
        "    float accum = s/4.0;" & vbLf &
        "    float prev = 0.0;" & vbLf &
        "    float tw = 0.0;" & vbLf &
        "    for (int i = 0; i < 26; ++i) {" & vbLf &
        "        float mag = dot(p, p);" & vbLf &
        "        p = abs(p) / mag + vec3(-0.5, -0.4, -1.5);" & vbLf &
        "        float w = exp(-float(i) / 7.0);" & vbLf &
        "        accum += w * exp(-strength * pow(abs(mag - prev), 2.2));" & vbLf &
        "        tw += w;" & vbLf &
        "        prev = mag;" & vbLf &
        "    }" & vbLf &
        "    return max(0.0, 5.0 * accum / tw - 0.7);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float field2(in vec3 p, float s) {" & vbLf &
        "    float strength = 7.0 + 0.03 * log(1.e-6 + fract(sin(iTime) * 4373.11));" & vbLf &
        "    float accum = s/4.0;" & vbLf &
        "    float prev = 0.0;" & vbLf &
        "    float tw = 0.0;" & vbLf &
        "    for (int i = 0; i < 18; ++i) {" & vbLf &
        "        float mag = dot(p, p);" & vbLf &
        "        p = abs(p) / mag + vec3(-0.5, -0.4, -1.5);" & vbLf &
        "        float w = exp(-float(i) / 7.0);" & vbLf &
        "        accum += w * exp(-strength * pow(abs(mag - prev), 2.2));" & vbLf &
        "        tw += w;" & vbLf &
        "        prev = mag;" & vbLf &
        "    }" & vbLf &
        "    return max(0.0, 5.0 * accum / tw - 0.7);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "vec3 nrand3( vec2 co ) {" & vbLf &
        "    vec3 a = fract( cos( co.x*8.3e-3 + co.y )*vec3(1.3e5, 4.7e5, 2.9e5) );" & vbLf &
        "    vec3 b = fract( sin( co.x*0.3e-3 + co.y )*vec3(8.1e5, 1.0e5, 0.1e5) );" & vbLf &
        "    vec3 c = mix(a, b, 0.5);" & vbLf &
        "    return c;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    vec2 uv = 2.0 * gl_FragCoord.xy / iResolution.xy - 1.0;" & vbLf &
        "    vec2 uvs = uv * iResolution.xy / max(iResolution.x, iResolution.y);" & vbLf &
        "    vec3 p = vec3(uvs / 4.0, 0.0) + vec3(1.0, -1.3, 0.0);" & vbLf &
        "    p += 0.2 * vec3(sin(iTime / 16.0), sin(iTime / 12.0), sin(iTime / 128.0));" & vbLf &
        "    " & vbLf &
        "    float freqs[4];" & vbLf &
        "    freqs[0] = noise(vec3( 0.01*100.0, 0.25 ,iTime/10.0) );" & vbLf &
        "    freqs[1] = noise(vec3( 0.07*100.0, 0.25 ,iTime/10.0) );" & vbLf &
        "    freqs[2] = noise(vec3( 0.15*100.0, 0.25 ,iTime/10.0) );" & vbLf &
        "    freqs[3] = noise(vec3( 0.30*100.0, 0.25 ,iTime/10.0) );" & vbLf &
        "    " & vbLf &
        "    float t = field(p,freqs[2]);" & vbLf &
        "    float v = (1.0 - exp((abs(uv.x) - 1.0) * 6.0)) * (1.0 - exp((abs(uv.y) - 1.0) * 6.0));" & vbLf &
        "    " & vbLf &
        "    vec3 p2 = vec3(uvs / (4.0+sin(iTime*0.11)*0.2+0.2+sin(iTime*0.15)*0.3+0.4), 1.5) + vec3(2.0, -1.3, -1.0);" & vbLf &
        "    p2 += 0.25 * vec3(sin(iTime / 16.0), sin(iTime / 12.0), sin(iTime / 128.0));" & vbLf &
        "    float t2 = field2(p2,freqs[3]);" & vbLf &
        "    " & vbLf &
        "    vec3 col2 = getPalette(t2, phase, palette);" & vbLf &
        "    vec4 c2 = mix(0.4, 1.0, v) * vec4(1.3 * col2.r, 1.8 * col2.g, col2.b * freqs[0], t2);" & vbLf &
        "    " & vbLf &
        "    vec2 seed = p.xy * 2.0;" & vbLf &
        "    seed = floor(seed * iResolution.x);" & vbLf &
        "    vec3 rnd = nrand3( seed );" & vbLf &
        "    vec4 starcolor = vec4(pow(rnd.y,40.0));" & vbLf &
        "    " & vbLf &
        "    vec2 seed2 = p2.xy * 2.0;" & vbLf &
        "    seed2 = floor(seed2 * iResolution.x);" & vbLf &
        "    vec3 rnd2 = nrand3( seed2 );" & vbLf &
        "    starcolor += vec4(pow(rnd2.y,40.0));" & vbLf &
        "    " & vbLf &
        "    vec3 col1 = getPalette(t, phase, palette);" & vbLf &
        "    fragColor = mix(freqs[3]-0.3, 1.0, v) * vec4(1.5 * freqs[2] * col1.r, 1.2 * freqs[1] * col1.g, freqs[3] * col1.b, 1.0) + c2 + starcolor;" & vbLf &
        "}"

    Private ReadOnly PolkaDotFrag As String =
        "#version 330 core" & vbLf &
        "out vec4 fragColor;" & vbLf &
        "uniform float iTime;" & vbLf &
        "uniform vec2  iResolution;" & vbLf &
        "uniform float seed1, seed2, scale, phase;" & vbLf &
        "uniform int   palette;" & vbLf &
        "" & vbLf &
        PaletteGLSL &
        "const float Pi = 3.14159265359;" & vbLf &
        "" & vbLf &
        "#define clamp01(x) clamp(x, 0.0, 1.0)" & vbLf &
        "#define mad(x, a, b) ((x) * (a) + (b))" & vbLf &
        "#define rsqrt(x) inversesqrt(x)" & vbLf &
        "" & vbLf &
        "vec3 GammaEncode(vec3 x) { return pow(x, vec3(1.0 / 2.2)); }" & vbLf &
        "" & vbLf &
        "float SDFtoMask(float sdf) {" & vbLf &
        "    return sdf / length(vec2(dFdx(sdf), dFdy(sdf))) * 1.2;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float Hash(float v) {" & vbLf &
        "    return fract(sin(v) * 43758.5453);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float Hash(vec2 v) {" & vbLf &
        "    return Hash(v.y + v.x * 12.9898);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float Hash(vec3 v) {" & vbLf &
        "    return Hash(v.y + v.x * 12.9898 + v.z * 33.7311);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float Pow2(float v) { return v * v; }" & vbLf &
        "float Pow3(float v) { return v * v * v; }" & vbLf &
        "float SqrLen(vec2 v) { return dot(v, v); }" & vbLf &
        "" & vbLf &
        "float EvalIntensityCurve(vec2 id, float time) {" & vbLf &
        "    time += Hash(id.yx * 1.733 + seed2);" & vbLf &
        "    float iTimeVal = floor(time);" & vbLf &
        "    float fTime = fract(time);" & vbLf &
        "    float h = Hash(vec3(id, iTimeVal));" & vbLf &
        "    h *= h;" & vbLf &
        "    h *= h;" & vbLf &
        "    float falloff = 1.0 - Pow2(fTime * 2.0 - 1.0);" & vbLf &
        "    {" & vbLf &
        "        const float f = 100.0;" & vbLf &
        "        const float d = 1.0 / (exp2(f) - 1.0);" & vbLf &
        "        falloff = mad(exp2(falloff * f), d, -d);" & vbLf &
        "    }" & vbLf &
        "    return h * falloff;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float GlowKern3(float x, float s) {" & vbLf &
        "    return s / Pow3(1.0 + s * x);" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float EvalGlow3(vec2 uv, vec2 off, float time) {" & vbLf &
        "    vec2 iUV = floor(uv) + off;" & vbLf &
        "    vec2 fUV = fract(uv) - off;" & vbLf &
        "    vec2 fUV2 = fUV * 2.0 - 1.0;" & vbLf &
        "    float l = length(fUV2);" & vbLf &
        "    l = max(0.0, l - 0.7);" & vbLf &
        "    float glow = GlowKern3(l, 1.2);" & vbLf &
        "    glow = clamp01(glow);" & vbLf &
        "    glow *= clamp01(1.0 - Pow2(l * 0.25));" & vbLf &
        "    return EvalIntensityCurve(iUV.xy, time) * glow;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float PlotDot(vec2 sp, vec2 dp, float dr) {" & vbLf &
        "    float v = length(sp - dp);" & vbLf &
        "    float d = v - dr;" & vbLf &
        "    d /= length(vec2(dFdx(v), dFdy(v)));" & vbLf &
        "    d = clamp01(1.0 - d * 1.2);" & vbLf &
        "    return d;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "float EvalGlyph(vec2 uv, vec2 off, float time) {" & vbLf &
        "    vec2 iUV = floor(uv) + off;" & vbLf &
        "    vec2 fUV = fract(uv) - off;" & vbLf &
        "    vec2 fUV2 = fUV * 2.0 - 1.0;" & vbLf &
        "    float gMask = PlotDot(fUV2, vec2(0.0), 0.75);" & vbLf &
        "    return EvalIntensityCurve(iUV.xy, time) * gMask;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "vec3 EvalTile(vec2 uv, vec2 off, float time) {" & vbLf &
        "    vec2 iUV = floor(uv) + off;" & vbLf &
        "    float glyph = EvalGlyph(uv, off, time);" & vbLf &
        "    float glow = 0.0;" & vbLf &
        "    for (float i = -2.0; i <= 2.0; ++i) {" & vbLf &
        "        for (float j = -2.0; j <= 2.0; ++j) {" & vbLf &
        "            glow += EvalGlow3(uv, off + vec2(i, j), time);" & vbLf &
        "        }" & vbLf &
        "    }" & vbLf &
        "    float intensity = mix(glow, glyph, 0.94) * 32.0;" & vbLf &
        "    vec3 baseCol = getPalette(Hash(iUV), phase, palette);" & vbLf &
        "    return baseCol * intensity;" & vbLf &
        "}" & vbLf &
        "" & vbLf &
        "void main() {" & vbLf &
        "    float gridScale = max(scale * 0.6, 1.2);" & vbLf &
        "    vec2 coord = (gl_FragCoord.xy - 0.5 * iResolution.xy) / gridScale;" & vbLf &
        "    float speed = 0.05 + 0.02 * seed1;" & vbLf &
        "    float time = iTime * speed;" & vbLf &
        "    vec3 outCol = EvalTile(coord, vec2(0.0), time);" & vbLf &
        "    fragColor = vec4(GammaEncode(clamp01(outCol)), 1.0);" & vbLf &
        "}"

End Module
