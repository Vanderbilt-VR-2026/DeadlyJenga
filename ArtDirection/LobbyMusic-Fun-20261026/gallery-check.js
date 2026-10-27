
const players=[...document.querySelectorAll('audio')];const loop=document.querySelector('#loop');function setLoops(){players.forEach(p=>p.loop=loop.checked)}setLoops();loop.addEventListener('change',setLoops);players.forEach(p=>p.addEventListener('play',()=>players.filter(q=>q!==p).forEach(q=>q.pause())));
