import Cocoa
import FlutterMacOS
import file_selector_macos
import printing

// A local trial launcher built with Command Line Tools, without a storyboard.
final class PreviewDelegate: FlutterAppDelegate {
  override func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
    return true
  }

  override func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
    return true
  }
}

let application = NSApplication.shared
application.setActivationPolicy(.regular)
let delegate = PreviewDelegate()
application.delegate = delegate

let menu = NSMenu()
let appItem = NSMenuItem()
menu.addItem(appItem)
let appMenu = NSMenu()
appMenu.addItem(withTitle: "إنهاء مسار", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
appItem.submenu = appMenu
let editItem = NSMenuItem()
editItem.title = "تحرير"
menu.addItem(editItem)
let editMenu = NSMenu(title: "تحرير")
for (title, action, key) in [
  ("تراجع", "undo:", "z"),
  ("قص", "cut:", "x"),
  ("نسخ", "copy:", "c"),
  ("لصق", "paste:", "v"),
  ("تحديد الكل", "selectAll:", "a")
] {
  editMenu.addItem(withTitle: title, action: Selector(action), keyEquivalent: key)
}
editItem.submenu = editMenu
application.mainMenu = menu
delegate.applicationMenu = appMenu

let controller = FlutterViewController()
FileSelectorPlugin.register(with: controller.registrar(forPlugin: "FileSelectorPlugin"))
PrintingPlugin.register(with: controller.registrar(forPlugin: "PrintingPlugin"))
let visible = NSScreen.main?.visibleFrame.size ?? NSSize(width: 1440, height: 900)
let window = NSWindow(
  contentRect: NSRect(x: 0, y: 0, width: min(1360, visible.width - 40), height: min(850, visible.height - 40)),
  styleMask: [.titled, .closable, .miniaturizable, .resizable],
  backing: .buffered,
  defer: false
)
// Attaching Flutter adopts its initial view size; keep the desktop window frame.
let windowFrame = window.frame
window.contentViewController = controller
window.setFrame(windowFrame, display: true)
window.minSize = NSSize(width: 960, height: 700)
window.title = "مسار | نادر جورج — تجربة ماك"
window.center()
delegate.mainFlutterWindow = window
window.makeKeyAndOrderFront(nil)
application.activate(ignoringOtherApps: true)
application.run()
