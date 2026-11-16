import numpy as np, json, sys
R=58.0
def project(p, pts):
    D,H,ly,fov,cx = p
    pos=np.array([cx,-D,H]); look=np.array([cx,ly,0.])
    f=look-pos; f/=np.linalg.norm(f)
    r=np.cross(f,[0,0,1]); r/=np.linalg.norm(r)
    u=np.cross(r,f)
    d=pts-pos
    foc=800/np.tan(np.radians(fov)/2)
    return 800+foc*(d@r)/(d@f), 450-foc*(d@u)/(d@f)
t=np.linspace(0,2*np.pi,1441)
circ=np.stack([R*np.cos(t),R*np.sin(t),0*t],1)
def solve(fov,target):
    q=np.array([110.,50.,0.,3.])
    def res(q):
        x,y=project([q[0],q[1],q[2],fov,q[3]],circ)
        return np.array([x.min(),x.max(),y.max(),y.min()])-target
    for _ in range(80):
        r0=res(q); J=np.zeros((4,4))
        for j in range(4):
            dq=q.copy(); dq[j]+=1e-3; J[:,j]=(res(dq)-r0)/1e-3
        q=q+np.clip(np.linalg.lstsq(J,-r0,rcond=None)[0],-10,10)
    return q
if __name__=="__main__":
    fov=float(sys.argv[1]); top=float(sys.argv[2])
    q=solve(fov,np.array([60,1590,880,top]))
    cfg=dict(pos=[float(q[3]),float(-q[0]),float(q[1])],look=[float(q[3]),float(q[2]),0.0],fov=fov)
    json.dump(cfg,open("cam.json","w")); print(cfg)
