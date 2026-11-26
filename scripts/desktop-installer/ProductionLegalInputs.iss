; This gate is not used by the synthetic harness. No test bypass in Production.
#ifndef AvelriTermsApproved
  #error Final legal terms require explicit human approval. Draft commercial use is blocked.
#endif
#if AvelriTermsApproved != "1"
  #error Final legal terms require explicit human approval. Draft commercial use is blocked.
#endif
#ifndef AvelriTermsFile
  #error AvelriTermsFile must point to the privately finalized approved terms.
#endif
#ifndef AvelriTermsRevision
  #error AvelriTermsRevision is required.
#endif
#ifndef AvelriTermsSha256
  #error AvelriTermsSha256 is required.
#endif
#ifndef AvelriThirdPartyNoticesDir
  #error AvelriThirdPartyNoticesDir is required.
#endif
#if LowerCase(GetSHA256OfFile(AvelriTermsFile)) != LowerCase(AvelriTermsSha256)
  #error Approved terms SHA-256 does not match the supplied document.
#endif
#define AvelriLegalRegistryKey "Software\Avelri\Gestao\Legal"
#define LegalTermsHandle FileOpen(AvelriTermsFile)
#define LegalTermsLine ""
#sub AvelriCheckLegalLine
  #expr LegalTermsLine = FileRead(LegalTermsHandle)
  #if Pos("AVELRI_LEGAL_DRAFT_DO_NOT_DISTRIBUTE", LegalTermsLine) > 0
    #error Draft commercial use is blocked even when the approval define is supplied.
  #endif
#endsub
#if LegalTermsHandle
  #for {""; !FileEof(LegalTermsHandle); ""} AvelriCheckLegalLine
  #expr FileClose(LegalTermsHandle)
#else
  #error Approved terms could not be read.
#endif
