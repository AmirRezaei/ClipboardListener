.\ClipboardListener.exe `
  --pattern '^(?i)https://x\.com/(?:[^/]+/status|i/(?:web/)?status)/\d+/photo/\d+(?:\?.*)?$' `
  --command gallery-dl `
  --parameter '--cookies-from-browser firefox -o twitter.size=orig "{clipboard}"' `
  --pause
