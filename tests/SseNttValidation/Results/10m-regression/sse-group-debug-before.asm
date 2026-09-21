; Assembly listing for method MathSolver.Numerics.ParallelBigUnsigned:ExecuteCachedGroupSse(uint[],uint,uint[],uint[],int,int,int,bool) (MinOpts)
; Emitting BLENDED_CODE for generic X64 + VEX + EVEX on Windows
; MinOpts code
; debuggable code
; rbp based frame
; fully interruptible
; compiling with minopt

G_M000_IG01:                ;; offset=0x0000
       push     rbp
       sub      rsp, 656
       lea      rbp, [rsp+0x290]
       xor      eax, eax
       mov      qword ptr [rbp-0x188], rax
       vxorps   xmm4, xmm4, xmm4
       mov      rax, -384
       vmovdqa  xmmword ptr [rax+rbp], xmm4
       vmovdqa  xmmword ptr [rbp+rax+0x10], xmm4
       vmovdqa  xmmword ptr [rbp+rax+0x20], xmm4
       add      rax, 48
       jne      SHORT  -5 instr
       mov      gword ptr [rbp+0x10], rcx
       mov      dword ptr [rbp+0x18], edx
       mov      gword ptr [rbp+0x20], r8
       mov      gword ptr [rbp+0x28], r9
 
G_M000_IG02:                ;; offset=0x004D
       cmp      dword ptr [(reloc 0x7ffd3192d238)], 0
       je       SHORT G_M000_IG04
 
G_M000_IG03:                ;; offset=0x0056
       call     CORINFO_HELP_DBG_IS_JUST_MY_CODE
 
G_M000_IG04:                ;; offset=0x005B
       nop      
       vpbroadcastd xmm0, dword ptr [rbp+0x18]
       vmovaps  xmmword ptr [rbp-0xD0], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0xD0]
       vmovaps  xmmword ptr [rbp-0x10], xmm0
       mov      rcx, gword ptr [rbp+0x10]
       call     [System.Runtime.InteropServices.MemoryMarshal:GetArrayDataReference[uint](uint[]):byref]
       mov      bword ptr [rbp-0xD8], rax
       mov      rax, bword ptr [rbp-0xD8]
       mov      bword ptr [rbp-0x18], rax
       mov      rcx, gword ptr [rbp+0x20]
       call     [System.Runtime.InteropServices.MemoryMarshal:GetArrayDataReference[uint](uint[]):byref]
       mov      bword ptr [rbp-0xE0], rax
       mov      rax, bword ptr [rbp-0xE0]
       mov      bword ptr [rbp-0x20], rax
       mov      rcx, gword ptr [rbp+0x28]
       call     [System.Runtime.InteropServices.MemoryMarshal:GetArrayDataReference[uint](uint[]):byref]
       mov      bword ptr [rbp-0xE8], rax
       mov      rax, bword ptr [rbp-0xE8]
       mov      bword ptr [rbp-0x28], rax
       xor      eax, eax
       mov      dword ptr [rbp-0x2C], eax
       nop      
       jmp      G_M000_IG08
 
G_M000_IG05:                ;; offset=0x00D4
       nop      
       mov      eax, dword ptr [rbp+0x30]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x18]
       vmovups  xmm0, xmmword ptr [rcx+4*rax]
       vmovaps  xmmword ptr [rbp-0x100], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x100]
       vmovaps  xmmword ptr [rbp-0x40], xmm0
       mov      eax, dword ptr [rbp+0x30]
       add      eax, dword ptr [rbp+0x38]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x18]
       vmovups  xmm0, xmmword ptr [rcx+4*rax]
       vmovaps  xmmword ptr [rbp-0x110], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x110]
       vmovaps  xmmword ptr [rbp-0x50], xmm0
       mov      eax, dword ptr [rbp+0x40]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x20]
       vmovups  xmm0, xmmword ptr [rcx+4*rax]
       vmovaps  xmmword ptr [rbp-0x120], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x120]
       vmovaps  xmmword ptr [rbp-0x60], xmm0
       mov      eax, dword ptr [rbp+0x40]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x28]
       vmovups  xmm0, xmmword ptr [rcx+4*rax]
       vmovaps  xmmword ptr [rbp-0x130], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x130]
       vmovaps  xmmword ptr [rbp-0x70], xmm0
       mov      eax, dword ptr [rbp+0x48]
       movzx    rax, al
       mov      dword ptr [rbp-0x94], eax
       cmp      dword ptr [rbp-0x94], 0
       je       SHORT G_M000_IG06
       vmovaps  xmm0, xmmword ptr [rbp-0x50]
       vmovaps  xmmword ptr [rbp-0x1A0], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x60]
       vmovaps  xmmword ptr [rbp-0x1B0], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x70]
       vmovaps  xmmword ptr [rbp-0x1C0], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x10]
       vmovaps  xmmword ptr [rbp-0x1D0], xmm0
       lea      rdx, [rbp-0x1A0]
       lea      r8, [rbp-0x1B0]
       lea      r9, [rbp-0x1C0]
       lea      rax, [rbp-0x1D0]
       mov      qword ptr [rsp+0x20], rax
       lea      rcx, [rbp-0x180]
       call     [MathSolver.Numerics.ParallelBigUnsigned:MultiplyShoupSse(System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint]):System.Runtime.Intrinsics.Vector128`1[uint]]
       vmovaps  xmm0, xmmword ptr [rbp-0x180]
       vmovaps  xmmword ptr [rbp-0x50], xmm0
 
G_M000_IG06:                ;; offset=0x01E7
       vmovaps  xmm0, xmmword ptr [rbp-0x40]
       vpaddd   xmm0, xmm0, xmmword ptr [rbp-0x50]
       vmovaps  xmmword ptr [rbp-0x140], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x140]
       vmovaps  xmmword ptr [rbp-0x1E0], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x10]
       vmovaps  xmmword ptr [rbp-0x1F0], xmm0
       lea      rdx, [rbp-0x1E0]
       lea      r8, [rbp-0x1F0]
       lea      rcx, [rbp-0x150]
       call     [MathSolver.Numerics.ParallelBigUnsigned:ReduceOnceSse(System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint]):System.Runtime.Intrinsics.Vector128`1[uint]]
       vmovaps  xmm0, xmmword ptr [rbp-0x150]
       vmovaps  xmmword ptr [rbp-0x80], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x40]
       vmovaps  xmmword ptr [rbp-0x200], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x50]
       vmovaps  xmmword ptr [rbp-0x210], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x10]
       vmovaps  xmmword ptr [rbp-0x220], xmm0
       lea      rdx, [rbp-0x200]
       lea      r8, [rbp-0x210]
       lea      r9, [rbp-0x220]
       lea      rcx, [rbp-0x160]
       call     [MathSolver.Numerics.ParallelBigUnsigned:SubtractModuloSse(System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint]):System.Runtime.Intrinsics.Vector128`1[uint]]
       vmovaps  xmm0, xmmword ptr [rbp-0x160]
       vmovaps  xmmword ptr [rbp-0x90], xmm0
       mov      eax, dword ptr [rbp+0x48]
       movzx    rax, al
       test     eax, eax
       sete     al
       movzx    rax, al
       mov      dword ptr [rbp-0x98], eax
       cmp      dword ptr [rbp-0x98], 0
       je       SHORT G_M000_IG07
       vmovaps  xmm0, xmmword ptr [rbp-0x90]
       vmovaps  xmmword ptr [rbp-0x230], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x60]
       vmovaps  xmmword ptr [rbp-0x240], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x70]
       vmovaps  xmmword ptr [rbp-0x250], xmm0
       vmovaps  xmm0, xmmword ptr [rbp-0x10]
       vmovaps  xmmword ptr [rbp-0x260], xmm0
       lea      rdx, [rbp-0x230]
       lea      r8, [rbp-0x240]
       lea      r9, [rbp-0x250]
       lea      rax, [rbp-0x260]
       mov      qword ptr [rsp+0x20], rax
       lea      rcx, [rbp-0x170]
       call     [MathSolver.Numerics.ParallelBigUnsigned:MultiplyShoupSse(System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint],System.Runtime.Intrinsics.Vector128`1[uint]):System.Runtime.Intrinsics.Vector128`1[uint]]
       vmovaps  xmm0, xmmword ptr [rbp-0x170]
       vmovaps  xmmword ptr [rbp-0x90], xmm0
 
G_M000_IG07:                ;; offset=0x0318
       mov      eax, dword ptr [rbp+0x30]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x18]
       vmovaps  xmm0, xmmword ptr [rbp-0x80]
       vmovups  xmmword ptr [rcx+4*rax], xmm0
       nop      
       mov      eax, dword ptr [rbp+0x30]
       add      eax, dword ptr [rbp+0x38]
       add      eax, dword ptr [rbp-0x2C]
       cdqe     
       mov      rcx, bword ptr [rbp-0x18]
       vmovaps  xmm0, xmmword ptr [rbp-0x90]
       vmovups  xmmword ptr [rcx+4*rax], xmm0
       nop      
       nop      
       mov      eax, dword ptr [rbp-0x2C]
       add      eax, 4
       mov      dword ptr [rbp-0x2C], eax
 
G_M000_IG08:                ;; offset=0x0355
       mov      eax, dword ptr [rbp+0x38]
       add      eax, -4
       cmp      dword ptr [rbp-0x2C], eax
       setle    al
       movzx    rax, al
       mov      dword ptr [rbp-0x9C], eax
       cmp      dword ptr [rbp-0x9C], 0
       jne      G_M000_IG05
       nop      
       jmp      G_M000_IG22
 
G_M000_IG09:                ;; offset=0x037D
       nop      
       mov      rax, gword ptr [rbp+0x10]
       mov      ecx, dword ptr [rbp+0x30]
       add      ecx, dword ptr [rbp-0x2C]
       cmp      ecx, dword ptr [rax+0x08]
       jb       SHORT G_M000_IG10
       call     CORINFO_HELP_RNGCHKFAIL
 
G_M000_IG10:                ;; offset=0x0392
       mov      edx, ecx
       lea      rax, bword ptr [rax+4*rdx+0x10]
       mov      eax, dword ptr [rax]
       mov      dword ptr [rbp-0xA0], eax
       mov      rax, gword ptr [rbp+0x10]
       mov      ecx, dword ptr [rbp+0x30]
       add      ecx, dword ptr [rbp+0x38]
       add      ecx, dword ptr [rbp-0x2C]
       cmp      ecx, dword ptr [rax+0x08]
       jb       SHORT G_M000_IG11
       call     CORINFO_HELP_RNGCHKFAIL
 
G_M000_IG11:                ;; offset=0x03B8
       mov      edx, ecx
       lea      rax, bword ptr [rax+4*rdx+0x10]
       mov      eax, dword ptr [rax]
       mov      dword ptr [rbp-0xA4], eax
       cmp      dword ptr [rbp+0x38], 1
       je       SHORT G_M000_IG13
       mov      rax, gword ptr [rbp+0x20]
       mov      ecx, dword ptr [rbp+0x40]
       add      ecx, dword ptr [rbp-0x2C]
       cmp      ecx, dword ptr [rax+0x08]
       jb       SHORT G_M000_IG12
       call     CORINFO_HELP_RNGCHKFAIL
 
G_M000_IG12:                ;; offset=0x03E1
       mov      edx, ecx
       lea      rax, bword ptr [rax+4*rdx+0x10]
       mov      eax, dword ptr [rax]
       mov      dword ptr [rbp-0x184], eax
       jmp      SHORT G_M000_IG14
 
G_M000_IG13:                ;; offset=0x03F2
       mov      dword ptr [rbp-0x184], 1
 
G_M000_IG14:                ;; offset=0x03FC
       mov      eax, dword ptr [rbp-0x184]
       mov      dword ptr [rbp-0xA8], eax
       mov      eax, dword ptr [rbp+0x48]
       movzx    rax, al
       mov      dword ptr [rbp-0xB4], eax
       cmp      dword ptr [rbp-0xB4], 0
       je       SHORT G_M000_IG15
       mov      eax, dword ptr [rbp-0xA4]
       mov      eax, eax
       mov      ecx, dword ptr [rbp-0xA8]
       mov      ecx, ecx
       imul     rax, rcx
       mov      ecx, dword ptr [rbp+0x18]
       mov      ecx, ecx
       xor      edx, edx
       div      rdx:rax, rcx
       mov      eax, edx
       mov      dword ptr [rbp-0xA4], eax
 
G_M000_IG15:                ;; offset=0x0443
       mov      eax, dword ptr [rbp-0xA0]
       add      eax, dword ptr [rbp-0xA4]
       mov      dword ptr [rbp-0xAC], eax
       mov      eax, dword ptr [rbp-0xAC]
       cmp      eax, dword ptr [rbp+0x18]
       setae    al
       movzx    rax, al
       mov      dword ptr [rbp-0xB8], eax
       cmp      dword ptr [rbp-0xB8], 0
       je       SHORT G_M000_IG16
       mov      eax, dword ptr [rbp-0xAC]
       sub      eax, dword ptr [rbp+0x18]
       mov      dword ptr [rbp-0xAC], eax
 
G_M000_IG16:                ;; offset=0x0482
       mov      eax, dword ptr [rbp-0xA0]
       cmp      eax, dword ptr [rbp-0xA4]
       jae      SHORT G_M000_IG17
       mov      eax, dword ptr [rbp-0xA0]
       add      eax, dword ptr [rbp+0x18]
       sub      eax, dword ptr [rbp-0xA4]
       mov      dword ptr [rbp-0x188], eax
       jmp      SHORT G_M000_IG18
 
G_M000_IG17:                ;; offset=0x04A7
       mov      eax, dword ptr [rbp-0xA0]
       sub      eax, dword ptr [rbp-0xA4]
       mov      dword ptr [rbp-0x188], eax
 
G_M000_IG18:                ;; offset=0x04B9
       mov      eax, dword ptr [rbp-0x188]
       mov      dword ptr [rbp-0xB0], eax
       mov      eax, dword ptr [rbp+0x48]
       movzx    rax, al
       test     eax, eax
       sete     al
       movzx    rax, al
       mov      dword ptr [rbp-0xBC], eax
       cmp      dword ptr [rbp-0xBC], 0
       je       SHORT G_M000_IG19
       mov      eax, dword ptr [rbp-0xB0]
       mov      eax, eax
       mov      ecx, dword ptr [rbp-0xA8]
       mov      ecx, ecx
       imul     rax, rcx
       mov      ecx, dword ptr [rbp+0x18]
       mov      ecx, ecx
       xor      edx, edx
       div      rdx:rax, rcx
       mov      eax, edx
       mov      dword ptr [rbp-0xB0], eax
 
G_M000_IG19:                ;; offset=0x0508
       mov      rax, gword ptr [rbp+0x10]
       mov      ecx, dword ptr [rbp+0x30]
       add      ecx, dword ptr [rbp-0x2C]
       cmp      ecx, dword ptr [rax+0x08]
       jb       SHORT G_M000_IG20
       call     CORINFO_HELP_RNGCHKFAIL
 
G_M000_IG20:                ;; offset=0x051C
       mov      edx, ecx
       lea      rax, bword ptr [rax+4*rdx+0x10]
       mov      ecx, dword ptr [rbp-0xAC]
       mov      dword ptr [rax], ecx
       mov      rax, gword ptr [rbp+0x10]
       mov      ecx, dword ptr [rbp+0x30]
       add      ecx, dword ptr [rbp+0x38]
       add      ecx, dword ptr [rbp-0x2C]
       cmp      ecx, dword ptr [rax+0x08]
       jb       SHORT G_M000_IG21
       call     CORINFO_HELP_RNGCHKFAIL
 
G_M000_IG21:                ;; offset=0x0542
       mov      edx, ecx
       lea      rax, bword ptr [rax+4*rdx+0x10]
       mov      ecx, dword ptr [rbp-0xB0]
       mov      dword ptr [rax], ecx
       nop      
       mov      eax, dword ptr [rbp-0x2C]
       inc      eax
       mov      dword ptr [rbp-0x2C], eax
 
G_M000_IG22:                ;; offset=0x055A
       mov      eax, dword ptr [rbp-0x2C]
       cmp      eax, dword ptr [rbp+0x38]
       setl     al
       movzx    rax, al
       mov      dword ptr [rbp-0xC0], eax
       cmp      dword ptr [rbp-0xC0], 0
       jne      G_M000_IG09
       nop      
 
G_M000_IG23:                ;; offset=0x057A
       add      rsp, 656
       pop      rbp
       ret      
 
; Total bytes of code 1411

