"""Build the diagnostic APK with installed official Android/JDK tools, no Unity Editor."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

p=argparse.ArgumentParser()
p.add_argument('--sdk',required=True);p.add_argument('--jdk',required=True)
p.add_argument('--output',required=True);p.add_argument('--keystore',required=True)
args=p.parse_args()
sdk=Path(args.sdk);jdk=Path(args.jdk)/'bin';output=Path(args.output).resolve()
if output.exists():raise SystemExit('Refusing to replace existing APK')
output.parent.mkdir(parents=True,exist_ok=True)
work=Path(tempfile.mkdtemp(prefix='bluetooth-probe-',dir=output.parent))
source=Path(__file__).resolve().parent
bridge=source.parents[1]/'Assets/_DeadlyJenga/GameSession/Session/Plugins/Android/NearbyBluetooth.java'
build=sdk/'build-tools/36.0.0';android=sdk/'platforms/android-36/android.jar'
classes=work/'classes';classes.mkdir();dex=work/'dex';dex.mkdir()
def run(*cmd):
    result=subprocess.run([str(x) for x in cmd],capture_output=True,text=True)
    with (work/'build.log').open('a',encoding='utf-8') as log:log.write(result.stdout+result.stderr+'\n')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return result.stdout+result.stderr
run(jdk/'javac.exe','-encoding','UTF-8','-source','8','-target','8','-cp',android,'-d',classes,bridge,source/'ProbeActivity.java')
run(jdk/'java.exe','-cp',build/'lib/d8.jar','com.android.tools.r8.D8','--lib',android,'--min-api','32','--output',dex,*classes.rglob('*.class'))
unsigned=work/'unsigned.apk';aligned=work/'aligned.apk'
run(build/'aapt2.exe','link','--manifest',source/'AndroidManifest.xml','-I',android,'-o',unsigned)
with zipfile.ZipFile(unsigned,'a',zipfile.ZIP_DEFLATED) as archive:archive.write(dex/'classes.dex','classes.dex')
run(build/'zipalign.exe','-p','-f','4',unsigned,aligned)
run(jdk/'java.exe','-jar',build/'lib/apksigner.jar','sign','--ks',args.keystore,'--ks-pass','pass:android','--key-pass','pass:android','--out',output,aligned)
verify=run(jdk/'java.exe','-jar',build/'lib/apksigner.jar','verify','--verbose','--print-certs',output)
permissions=run(build/'aapt2.exe','dump','permissions',output)
for name in ['BLUETOOTH_SCAN','BLUETOOTH_ADVERTISE','BLUETOOTH_CONNECT']:
    if 'android.permission.'+name not in permissions:raise RuntimeError('Missing permission '+name)
with zipfile.ZipFile(output) as archive:
    data=archive.read('classes.dex')
    if b'Lcom/deadlyjenga/nearby/NearbyBluetooth;' not in data:raise RuntimeError('Native bridge absent from dex')
receipt={'apk':str(output),'sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'bytes':output.stat().st_size,'bridgeSha256':hashlib.sha256(bridge.read_bytes()).hexdigest(),'signatureVerification':verify,'permissions':permissions,'limits':'Diagnostic APK only; no Unity UI/Photon gameplay or Quest hardware test.'}
output.with_suffix('.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in receipt.items() if k not in ['signatureVerification','permissions']},indent=2))
