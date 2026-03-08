# Milo's Music Player

A fast, modern desktop music player built with **C#**, **Avalonia UI**, and **NAudio**.

This project focuses on a clean interface, smooth playback, and experimental UI features.
---

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/f6f6d810-cd1f-47e1-9d7c-0583a693ce53" />

## Features

* Music playback using **NAudio**
* Automatic scanning of the user's **Music folder**
* Album art extraction using **TagLibSharp**
* Real-time **progress slider with interpolation**
* Futuristic UI built with **Avalonia**
* Lightweight and fast... kinda? i think? please help me improve this if you think otherwise!

---


## How It Works

The player scans the user's system music directory:

```
C:\Users\<username>\Music
```

Songs are loaded into a library and played using NAudio. Metadata such as title, artist, genre, and album art are extracted using TagLib.
---

## Libraries Used

* **Avalonia UI**
* **NAudio**
* **TagLibSharp**
* **Skia / GPU Shader Rendering**

---

## Building

Clone the repository:

```
git clone https://github.com/NovaAshwolfDev/MiloMusicPlayer.git
```

Navigate to the project folder:

```
cd MiloMusicPlayer
```

Run the project:

```
dotnet run
```

---

## Planned Features
* Settings panel
---

## License

This project is open source and available under the **MIT License**.
