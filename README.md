# WANG

**Write programs by moving real Windows windows.** Their size, overlap and stacking are the program.

## Run

Extract the release ZIP and run `WANG/app/WANG.exe`. `.NET` is included. The optional `Install.ps1` installs it for your user account.

Create blank windows with **New window**, or use ordinary apps. Newly opened apps join automatically (their contents are never altered); **Window actions** can capture or release an existing one.

Several example programs are available under **Examples** and in [examples/](examples/).

## How it works

**Width gives a starting number:** 400 pixels means 0; every 8 pixels adds 1. Shrink for negative numbers. The displayed number is the current answer, which can differ from that starting value.

**Overlap connects windows.** Put inputs partly behind a receiving window. Their centers determine the direction; the nearest input behind the receiver selects the operation. Leave an 8–24 pixel gap instead to compare numbers:

| Input's side | Overlap | Gap |
| --- | --- | --- |
| Left | Add | Less than |
| Right | Subtract | Equal |
| Above | Multiply | Greater than |
| Below | Divide | Less than or equal |

Comparisons return **1 for yes, 0 for no**. The number of inputs determines the receiver's role:

| Inputs | Result |
| --- | --- |
| 0 | Its own width number |
| 1 | Combine its width number with the input |
| 2 | Combine the further input with the nearest one |
| 3 | Choose: condition, yes answer, no answer |
| 4 | Draw a rectangle: X, Y, width, height |

Input order is **nearest to furthest behind the receiver**. Any nonzero condition selects yes. Rectangles appear together in **Output**, using coordinates 100 units across and 60 down. The frontmost source supplies the page's numeric output.

A window 320–1347 pixels tall becomes a **memory jar**. Every four pixels selects another jar; matching jar numbers share a value across pages. 
Jars 4–256 store your numbers, starting with Jar 4 at 336–339 pixels.

Touch a calculation to a jar's left edge to **store its answer next step**, or to its right edge to **share it immediately**. 
The destination label confirms the connection.

| Built-in jar | Reads | Height |
| --- | --- | --- |
| 0 | Mouse X: 0–100 | 320–323 px |
| 1 | Mouse Y: 0–60 | 324–327 px |
| 2 | Space: 1 while held | 328–331 px |
| 3 | Time per tick: 1/60 second | 332–335 px |

Mouse and Space input come from **Output**. Ordinary apps often open at jar height; resize below 320 pixels for a plain width-driven number.

**Minimize** disconnects a window; **restore** reconnects it. **Close** removes it. Stacking changes input order; windows piled at nearly the same top-left position hold their own values, with Push/Pop under Window actions.

## Edit and run

**Step** advances once. 
**Live/Pause** starts or stops repetition. 
**Reset** restores starting values and pauses. 
**Undo/Redo** restores arrangements while keeping current memory.

Pages separate groups of windows while the whole program runs. Matching jars connect pages; ordinary overlaps stay on their own page. Create, rename and browse pages from the controls, or use **All pages overview**.

**Save/Open** uses `.wang` files: arrangements, page names and starting values, excluding running memory and other apps' documents. 
**Help** explains the rules; 
**Output** shows results and errors.
**Log** records editor failures.

## Build

With the **.NET 10 SDK** on Windows:

```powershell
.\Build.ps1
```

Builds and packages a self-contained release under `artifacts/`. Use `-NoPackage` to build without packaging. Run locally with `dotnet run --project src/WANG`.
