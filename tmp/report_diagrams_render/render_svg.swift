import AppKit
import WebKit

final class Renderer: NSObject, WKNavigationDelegate {
    private let source: URL
    private let output: URL
    private let size: CGSize
    private var webView: WKWebView!
    private var finished = false

    init(source: URL, output: URL, size: CGSize) {
        self.source = source
        self.output = output
        self.size = size
    }

    func run() -> Bool {
        let configuration = WKWebViewConfiguration()
        webView = WKWebView(frame: CGRect(origin: .zero, size: size), configuration: configuration)
        webView.navigationDelegate = self
        webView.setValue(false, forKey: "drawsBackground")
        webView.loadFileURL(source, allowingReadAccessTo: source.deletingLastPathComponent())

        let deadline = Date().addingTimeInterval(20)
        while !finished && RunLoop.current.run(mode: .default, before: Date(timeIntervalSinceNow: 0.05)) && Date() < deadline {}
        return finished && FileManager.default.fileExists(atPath: output.path)
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        let config = WKSnapshotConfiguration()
        config.rect = CGRect(origin: .zero, size: size)
        config.snapshotWidth = NSNumber(value: Double(size.width))
        webView.takeSnapshot(with: config) { image, error in
            defer { self.finished = true }
            guard error == nil,
                  let image,
                  let tiff = image.tiffRepresentation,
                  let bitmap = NSBitmapImageRep(data: tiff),
                  let png = bitmap.representation(using: .png, properties: [:]) else { return }
            try? png.write(to: self.output)
        }
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        finished = true
    }
}

guard CommandLine.arguments.count == 5,
      let width = Double(CommandLine.arguments[3]),
      let height = Double(CommandLine.arguments[4]) else {
    fputs("usage: render_svg input.svg output.png width height\n", stderr)
    exit(2)
}

let renderer = Renderer(
    source: URL(fileURLWithPath: CommandLine.arguments[1]),
    output: URL(fileURLWithPath: CommandLine.arguments[2]),
    size: CGSize(width: width, height: height)
)
exit(renderer.run() ? 0 : 1)
