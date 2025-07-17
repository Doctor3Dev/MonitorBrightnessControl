# Monitor Brightness Control via DDC/CI monitor

I was pissed of with the new version of the recent Dell Monitor Display buggy, slow and unresponsive for just changing the brightness from my Windows (they also removed quick access from the taskbar system tray...), so I decided to vibe code this lightweight app in .NET 4.7 using old school WinForm without any extra fancy UI dependencies.

<img width="386" height="353" alt="image" src="https://github.com/user-attachments/assets/b0d6fc71-c2d5-48b0-b11a-d51bba98b8f4" />

- It Should work on Windows 10/11 with .NET 4.7 dependencies (if you don't have it, it should be installed automatically) and a compatible DDC/CI monitor.
- The app is not registered, and Windows may say it's potentially dangerous to install. If you have trust issues, you can just clone the repo and build by yourself with Visual Studio 2022.
- When checked 'Start with Windows', the app will be launched automatically and minimized into the taskbar system tray.
