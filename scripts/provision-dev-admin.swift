import Foundation
import LocalAuthentication
import Security

let account = "entrada-local"
let emailService = "dev.entrada.bootstrap.admin.email"
let passwordService = "dev.entrada.bootstrap.admin.password"

func exists(_ service: String) -> Bool {
    let context = LAContext()
    context.interactionNotAllowed = true
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: service,
        kSecAttrAccount as String: account,
        kSecMatchLimit as String: kSecMatchLimitOne,
        kSecUseAuthenticationContext as String: context
    ]
    return SecItemCopyMatching(query as CFDictionary, nil) == errSecSuccess
}

func save(_ value: String, service: String) {
    let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: service,
        kSecAttrAccount as String: account,
        kSecAttrLabel as String: "ENTRADA administrador de desarrollo",
        kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        kSecValueData as String: Data(value.utf8)
    ]
    guard SecItemAdd(query as CFDictionary, nil) == errSecSuccess else {
        fputs("No se pudo guardar la credencial de desarrollo en el llavero.\n", stderr)
        exit(1)
    }
}

let emailExists = exists(emailService)
let passwordExists = exists(passwordService)
if emailExists && passwordExists {
    print("La cuenta de desarrollo ya está provisionada en el llavero.")
    exit(0)
}
guard !emailExists && !passwordExists else {
    fputs("El llavero contiene una configuración incompleta del administrador.\n", stderr)
    exit(1)
}

var randomBytes = [UInt8](repeating: 0, count: 24)
guard SecRandomCopyBytes(kSecRandomDefault, randomBytes.count, &randomBytes) == errSecSuccess else {
    fputs("No se pudo generar una credencial segura.\n", stderr)
    exit(1)
}
let randomHex = randomBytes.map { String(format: "%02x", $0) }.joined()
save("admin-\(randomHex.prefix(12))@example.invalid", service: emailService)
save(randomHex, service: passwordService)
print("Administrador de desarrollo provisionado en el llavero.")
