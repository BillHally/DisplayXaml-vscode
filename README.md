# DisplayXaml for VS Code

Launches an external WPF viewer to provide a live preview of a given `XAML` file.

You can also specify an F# script file to use to generate the
`ViewModel` to set as the `DataContext`.

### Example Control.xaml

Any XAML that creates a `FrameworkElement` should work (note that creating a
`Window` is not yet supported).

```xml
<UserControl
	xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:mahapps="clr-namespace:MahApps.Metro.Controls;assembly=MahApps.Metro"
    Width="300"
    Height="200"
    >
    <Grid>
        <StackPanel>
            <TextBlock
                Text="{Binding Status}"
                Margin="10"
                FontSize="20"
                />

            <mahapps:ToggleSwitch
                IsChecked="{Binding B}"
                Margin="10"
                Width="100"
                OnLabel="On"
                OffLabel="Off"
                HorizontalAlignment="Left"
                Style="{StaticResource MahApps.Metro.Styles.ToggleSwitch.Win10}"
                />
        </StackPanel>
    </Grid>
</UserControl>
```

### Example ViewModel.fsx

Can contain any valid F#, but needs to provide the function:

```fsharp
getViewModel : unit -> 'a
```

For example:

```fsharp
#r "presentationframework"
#r "PresentationCore"
#r "WindowsBase"
#r "System.ObjectModel"
#r "System.Xaml"

#I __SOURCE_DIRECTORY__
#I "../packages"

#r @"FsXaml.Wpf\lib\net45\FsXaml.Wpf.dll"
#r @"FsXaml.Wpf\lib\net45\FsXaml.Wpf.typeprovider.dll"

#r @"ControlzEx\lib\net462\System.Windows.Interactivity.dll"
#r @"ControlzEx\lib\net462\ControlzEx.dll"
#r @"MahApps.Metro\lib\net45\MahApps.Metro.dll"

#r @"Gjallarhorn\lib\netstandard2.0\Gjallarhorn.dll"
#r @"Gjallarhorn.Bindable\lib\netstandard2.0\Gjallarhorn.Bindable.dll"
#r @"Gjallarhorn.Bindable.Wpf\lib\netstandard2.0\Gjallarhorn.Bindable.Wpf.dll"
#r @"FSharp.ViewModule.Core\lib\portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1\FSharp.ViewModule.dll"
#r @"System.Reactive.Core\lib\net46\System.Reactive.Core.dll"
#r @"System.Reactive.Interfaces\lib\net45\System.Reactive.Interfaces.dll"
#r @"System.Reactive.Linq\lib\net46\System.Reactive.Linq.dll"
#r @"System.Reactive.PlatformServices\lib\net46\System.Reactive.PlatformServices.dll"
#r @"System.Reactive.Windows.Threading\lib\net45\System.Reactive.Windows.Threading.dll"
#r @"FSharp.Control.Reactive/lib/net45/FSharp.Control.Reactive.dll"

open System.Reactive

open ViewModule
open ViewModule.FSharp

type ViewModel() as this =
    inherit ViewModelBase()

    let b = this.Factory.Backing(<@ this.B @>, false)

    let status = this.Factory.Backing(<@ this.Status @>, "<initial>")
    let n = this.Factory.Backing(<@ this.N @>, 0)

    let text = this.Factory.Backing(<@ this.Text @>, "some text")

    do
        b.Add
            (
                fun x ->
                    status.Value <- match x with true -> "On" | false -> "Off"
                    n.Value <- n.Value + 1
            )

    member __.Text with get () = text.Value and set v = text.Value <- v

    member __.N = n.Value

    member __.B with get () = b.Value and set v = b.Value <- v

    member __.Status = status.Value
let getViewModel () = ViewModel()
```

This extension adds 2 buttons to the status bar when you're displaying XAML files.

Click the `Preview XAML` one to launch the previewer. If you've got a suitable
F# script in the same workspace which you want to use to create `ViewModel`, you
can specify that using the `Set VM script` button.

------------

## Development

This repository was forked from [inosik/fable-vscode-rollup-sample](https://github.com/inosik/fable-vscode-rollup-sample).

*Original documentation from that repository follows:*

### Example VS Code Extension with Fable

This is the [Word Count example][example] build with [Fable][fable-home]. It's cloned from [acormier/vscode-fable-sample][upstream] and modified to work with the .NET Core 2 SDK, Rollup instead of Webpack, Paket instead of NuGet and Fable >= 1.2.0, which allows us to omit the `packages/` directory.

  [example]: https://code.visualstudio.com/docs/extensions/example-word-count
  [fable-home]: http://fable.io/
  [upstream]: https://github.com/acormier/vscode-fable-sample

#### Getting Started

Run the following commands:

- `yarn install`
- `.paket/paket.exe restore`
- `cd src/`
- `dotnet restore`
- `dotnet fable start`

Now you can open VS Code and hit <kbd>F5</kbd> to start another instance of VS Code, which will have this extension loaded and the debugger attached to it. When starting the "Launch Extension" configuration, VS Code will run the `build` NPM script, which will transpile and bundle the source code and write the resulting JavaScript code to the `out/` directory.

#### TODO

- Get the debugger to work with F# code

  When setting breakpoints in F# code, the execution stops at the correct location, *but the editor jumps to the transpiled JavaScript code*. Breakpoints in ES2015 code, as seen in the `src/formatter.js` file, work correctly.

- Start Rollup in watch mode

  That would allow editing the source code and simply reloading the VS Code extension host to see the changes.
