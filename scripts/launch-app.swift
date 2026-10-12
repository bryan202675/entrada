import Foundation
import Darwin
import Dispatch
import LocalAuthentication
import Security

func readKeychain(_ service: String) -> String? {
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

guard let secret = readKeychain("dev.entrada.sqlserver.sa") else {
    fputs("No se pudo leer la credencial local del llavero de macOS.\n", stderr)
    exit(1)
}

var environment = ProcessInfo.processInfo.environment
environment["ConnectionStrings__Entrada"] = "Server=127.0.0.1,1433;Database=EntradaDb;User ID=sa;Password=\(secret);Encrypt=True;TrustServerCertificate=True;Connection Timeout=3"
environment["ASPNETCORE_ENVIRONMENT"] = "Development"
environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5187"
if let adminEmail = readKeychain("dev.entrada.bootstrap.admin.email"),
   let adminPassword = readKeychain("dev.entrada.bootstrap.admin.password") {
    environment["BootstrapAdmin__Email"] = adminEmail
    environment["BootstrapAdmin__Password"] = adminPassword
}

let app = Process()
app.executableURL = URL(fileURLWithPath: "/usr/bin/env")
app.arguments = ["dotnet", "bin/Debug/net8.0/Entrada.dll"]
app.environment = environment
app.standardInput = FileHandle.standardInput
app.standardOutput = FileHandle.standardOutput
app.standardError = FileHandle.standardError

do {
    try app.run()
    var signalSources: [DispatchSourceSignal] = []
    for signalNumber in [SIGINT, SIGTERM] {
        signal(signalNumber, SIG_IGN)
        let source = DispatchSource.makeSignalSource(signal: signalNumber, queue: .global())
        source.setEventHandler {
            if app.isRunning { app.terminate() }
        }
        source.resume()
        signalSources.append(source)
    }
    app.waitUntilExit()
    signalSources.forEach { $0.cancel() }
    exit(app.terminationStatus)
} catch {
    fputs("No se pudo iniciar la aplicación ASP.NET.\n", stderr)
    exit(1)
}
