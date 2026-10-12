import Foundation
import LocalAuthentication
import Security

func credential(_ service: String) -> String? {
    let context = LAContext()
    context.interactionNotAllowed = true
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: service,
        kSecAttrAccount as String: "entrada-local",
        kSecReturnData as String: true,
        kSecMatchLimit as String: kSecMatchLimitOne,
        kSecUseAuthenticationContext as String: context
    ]
    var item: CFTypeRef?
    guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
          let data = item as? Data else { return nil }
    return String(data: data, encoding: .utf8)
}

guard let email = credential("dev.entrada.bootstrap.admin.email"),
      let password = credential("dev.entrada.bootstrap.admin.password") else {
    fputs("Faltan las credenciales de desarrollo en el llavero.\n", stderr)
    exit(1)
}

let base = URL(string: "http://127.0.0.1:5187")!
let session = URLSession(configuration: .ephemeral)
let loginURL = base.appending(path: "cuenta/iniciar-sesion")
let (loginData, loginResponse) = try await session.data(from: loginURL)
guard (loginResponse as? HTTPURLResponse)?.statusCode == 200,
      let html = String(data: loginData, encoding: .utf8) else {
    fputs("No se pudo abrir el formulario de acceso.\n", stderr)
    exit(1)
}

let tokenPattern = try NSRegularExpression(pattern: #"name="__RequestVerificationToken" type="hidden" value="([^"]+)""#)
let range = NSRange(html.startIndex..<html.endIndex, in: html)
guard let match = tokenPattern.firstMatch(in: html, range: range),
      let tokenRange = Range(match.range(at: 1), in: html) else {
    fputs("No se encontró el token de seguridad del formulario.\n", stderr)
    exit(1)
}

var form = URLComponents()
form.queryItems = [
    URLQueryItem(name: "Email", value: email),
    URLQueryItem(name: "Password", value: password),
    URLQueryItem(name: "__RequestVerificationToken", value: String(html[tokenRange]))
]
var request = URLRequest(url: loginURL)
request.httpMethod = "POST"
request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
request.httpBody = form.percentEncodedQuery?.data(using: .utf8)
let (_, signInResponse) = try await session.data(for: request)
guard signInResponse.url?.path == "/" else {
    fputs("El administrador no pudo iniciar sesión.\n", stderr)
    exit(1)
}

for route in ["admin", "admin/reservas", "admin/acceso"] {
    let (_, response) = try await session.data(from: base.appending(path: route))
    let status = (response as? HTTPURLResponse)?.statusCode ?? 0
    print("/\(route): \(status)")
    guard status == 200 else { exit(1) }
}
