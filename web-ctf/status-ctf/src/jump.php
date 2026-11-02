<?php
require __DIR__ . '/flag.inc.php';
header('X-CTF-Flag: ' . ctf_flag());
header('Location: /landing.php', true, 302);
exit;
