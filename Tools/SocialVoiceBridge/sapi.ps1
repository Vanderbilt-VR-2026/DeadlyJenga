$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$request = [Console]::In.ReadToEnd() | ConvertFrom-Json
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $voices = @($synth.GetInstalledVoices() | Where-Object Enabled | ForEach-Object { $_.VoiceInfo.Name })
    if ($request.action -eq 'voices') {
        @{ voices = $voices } | ConvertTo-Json -Compress
    } else {
        if ($voices.Count -eq 0) { throw 'No installed Windows speech voices.' }
        $index = [Math]::Abs([int]$request.voiceIndex) % $voices.Count
        $synth.SelectVoice($voices[$index])
        $synth.Rate = [Math]::Max(-3, [Math]::Min(3, [int]$request.rate))
        $stream = New-Object System.IO.MemoryStream
        $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
        $synth.SetOutputToAudioStream($stream, $format)
        $synth.Speak([string]$request.text)
        @{ audio = [Convert]::ToBase64String($stream.ToArray()); sampleRate = 16000; voice = $voices[$index] } | ConvertTo-Json -Compress
        $stream.Dispose()
    }
} finally { $synth.Dispose() }
