$keys = @("custom-b64-ctf","rev-anti-debug","rev-flip-branch","rev-hello-decompile","rev-hello-toolchain","rev-math-check","rev-rc4","rev-serial-eq","rev-smc","rev-strings","rev-tiny-vm","rev-upx","rev-xor-loop")
$i = 0
foreach ($k in $keys) {
  $i++
  $out = (python make_mapped.py $k 32 2>&1) -join "`n"
  if ($out -match "校验：全部通过|一致") { $s = "OK" } else { $s = "check" }
  "{0}/13  {1,-24} {2}" -f $i, $k, $s
}