// Trade of MTBS 3D — iOS'ta Apple ve Google ile giriş.
// Apple: ASAuthorizationAppleIDProvider (identityToken → /tmtbs/auth/apple, aud = Bundle ID).
// Google: ASWebAuthenticationSession + PKCE ile iOS OAuth istemcisi; kod token uç noktasında id_token'a çevrilir
// (aud = iOS istemci kimliği, sunucuda google_client_ids listesinde olmalı).
// Sonuç Unity'ye "SocialLogin" nesnesinin OnSocial yöntemiyle "sağlayıcı\1jeton\1ad\1hata" olarak gider.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <CommonCrypto/CommonDigest.h>
#import <Security/Security.h>

extern void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern UIViewController *UnityGetGLViewController(void);

static void TmSend(NSString *provider, NSString *token, NSString *name, NSString *error)
{
    NSString *m = [NSString stringWithFormat:@"%@\x01%@\x01%@\x01%@", provider ?: @"", token ?: @"", name ?: @"", error ?: @""];
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("SocialLogin", "OnSocial", [m UTF8String]);
    });
}

static NSString *TmB64Url(NSData *d)
{
    NSString *s = [d base64EncodedStringWithOptions:0];
    s = [s stringByReplacingOccurrencesOfString:@"+" withString:@"-"];
    s = [s stringByReplacingOccurrencesOfString:@"/" withString:@"_"];
    return [s stringByReplacingOccurrencesOfString:@"=" withString:@""];
}

static NSString *TmRandom(void)
{
    uint8_t b[32];
    if (SecRandomCopyBytes(kSecRandomDefault, sizeof(b), b) != errSecSuccess) arc4random_buf(b, sizeof(b));
    return TmB64Url([NSData dataWithBytes:b length:sizeof(b)]);
}

static NSString *TmEnc(NSString *s)
{
    NSMutableCharacterSet *ok = [[NSCharacterSet alphanumericCharacterSet] mutableCopy];
    [ok addCharactersInString:@"-._~"];
    return [s stringByAddingPercentEncodingWithAllowedCharacters:ok];
}

API_AVAILABLE(ios(13.0))
@interface TmSocialAuth : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding, ASWebAuthenticationPresentationContextProviding>
@property (nonatomic, strong) ASWebAuthenticationSession *session;
@end

@implementation TmSocialAuth

+ (instancetype)shared
{
    static TmSocialAuth *s;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ s = [TmSocialAuth new]; });
    return s;
}

- (UIWindow *)tmWindow
{
    UIWindow *w = UnityGetGLViewController().view.window;
    return w ?: UIApplication.sharedApplication.windows.firstObject;
}

// ---------------------------------------------------------------- Apple

- (void)apple
{
    ASAuthorizationAppleIDRequest *r = [[ASAuthorizationAppleIDProvider new] createRequest];
    r.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];
    ASAuthorizationController *c = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[r]];
    c.delegate = self;
    c.presentationContextProvider = self;
    [c performRequests];
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller
{
    return [self tmWindow];
}

- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithAuthorization:(ASAuthorization *)authorization
{
    ASAuthorizationAppleIDCredential *c = (ASAuthorizationAppleIDCredential *)authorization.credential;
    if (![c isKindOfClass:[ASAuthorizationAppleIDCredential class]] || c.identityToken == nil) {
        TmSend(@"apple", @"", @"", @"Apple girişi tamamlanamadı");
        return;
    }
    NSString *tok = [[NSString alloc] initWithData:c.identityToken encoding:NSUTF8StringEncoding];
    NSMutableArray *parts = [NSMutableArray array];
    if (c.fullName.givenName.length) [parts addObject:c.fullName.givenName];
    if (c.fullName.familyName.length) [parts addObject:c.fullName.familyName];
    TmSend(@"apple", tok, [parts componentsJoinedByString:@" "], @"");
}

- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithError:(NSError *)error
{
    BOOL cancel = error.code == ASAuthorizationErrorCanceled;
    TmSend(@"apple", @"", @"", cancel ? @"iptal" : error.localizedDescription);
}

// ---------------------------------------------------------------- Google (PKCE)

- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)session
{
    return [self tmWindow];
}

- (void)google:(NSString *)clientId
{
    if (clientId.length == 0) { TmSend(@"google", @"", @"", @"Google istemcisi tanımlı değil"); return; }
    NSString *prefix = [clientId stringByReplacingOccurrencesOfString:@".apps.googleusercontent.com" withString:@""];
    NSString *scheme = [@"com.googleusercontent.apps." stringByAppendingString:prefix];
    NSString *redirect = [scheme stringByAppendingString:@":/oauth2redirect"];
    NSString *verifier = TmRandom();
    NSString *state = TmRandom();
    NSData *vd = [verifier dataUsingEncoding:NSASCIIStringEncoding];
    uint8_t hash[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(vd.bytes, (CC_LONG)vd.length, hash);
    NSString *challenge = TmB64Url([NSData dataWithBytes:hash length:sizeof(hash)]);

    NSString *url = [NSString stringWithFormat:
        @"https://accounts.google.com/o/oauth2/v2/auth?client_id=%@&redirect_uri=%@&response_type=code&scope=%@&code_challenge=%@&code_challenge_method=S256&state=%@&prompt=select_account",
        TmEnc(clientId), TmEnc(redirect), TmEnc(@"openid email profile"), challenge, state];

    __weak TmSocialAuth *weak = self;
    self.session = [[ASWebAuthenticationSession alloc] initWithURL:[NSURL URLWithString:url] callbackURLScheme:scheme
        completionHandler:^(NSURL *cb, NSError *err) {
            weak.session = nil;
            if (err) {
                BOOL cancel = err.code == ASWebAuthenticationSessionErrorCodeCanceledLogin;
                TmSend(@"google", @"", @"", cancel ? @"iptal" : err.localizedDescription);
                return;
            }
            NSURLComponents *q = [NSURLComponents componentsWithURL:cb resolvingAgainstBaseURL:NO];
            NSString *code = nil, *st = nil, *e = nil;
            for (NSURLQueryItem *i in q.queryItems) {
                if ([i.name isEqualToString:@"code"]) code = i.value;
                else if ([i.name isEqualToString:@"state"]) st = i.value;
                else if ([i.name isEqualToString:@"error"]) e = i.value;
            }
            if (e) { TmSend(@"google", @"", @"", [e isEqualToString:@"access_denied"] ? @"iptal" : e); return; }
            if (!code || ![st isEqualToString:state]) { TmSend(@"google", @"", @"", @"Google girişi tamamlanamadı"); return; }
            [weak exchange:code clientId:clientId redirect:redirect verifier:verifier];
        }];
    self.session.presentationContextProvider = self;
    self.session.prefersEphemeralWebBrowserSession = NO;
    if (![self.session start]) TmSend(@"google", @"", @"", @"Google penceresi açılamadı");
}

- (void)exchange:(NSString *)code clientId:(NSString *)clientId redirect:(NSString *)redirect verifier:(NSString *)verifier
{
    NSMutableURLRequest *r = [NSMutableURLRequest requestWithURL:[NSURL URLWithString:@"https://oauth2.googleapis.com/token"]];
    r.HTTPMethod = @"POST";
    [r setValue:@"application/x-www-form-urlencoded" forHTTPHeaderField:@"Content-Type"];
    NSString *body = [NSString stringWithFormat:@"code=%@&client_id=%@&redirect_uri=%@&code_verifier=%@&grant_type=authorization_code",
        TmEnc(code), TmEnc(clientId), TmEnc(redirect), TmEnc(verifier)];
    r.HTTPBody = [body dataUsingEncoding:NSUTF8StringEncoding];
    [[[NSURLSession sharedSession] dataTaskWithRequest:r completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err) {
        if (err || !data) { TmSend(@"google", @"", @"", err.localizedDescription ?: @"Bağlantı hatası"); return; }
        NSDictionary *j = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        NSString *idt = [j isKindOfClass:[NSDictionary class]] ? j[@"id_token"] : nil;
        if (![idt isKindOfClass:[NSString class]] || idt.length == 0) {
            NSString *msg = [j isKindOfClass:[NSDictionary class]] ? (j[@"error_description"] ?: j[@"error"]) : nil;
            TmSend(@"google", @"", @"", msg ?: @"Google girişi tamamlanamadı");
            return;
        }
        TmSend(@"google", idt, @"", @"");
    }] resume];
}

@end

extern "C" {
    void TmSocialApple(void)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (@available(iOS 13.0, *)) [[TmSocialAuth shared] apple];
            else TmSend(@"apple", @"", @"", @"iOS 13 gerekli");
        });
    }

    void TmSocialGoogle(const char *iosClientId)
    {
        NSString *cid = iosClientId ? [NSString stringWithUTF8String:iosClientId] : @"";
        dispatch_async(dispatch_get_main_queue(), ^{
            if (@available(iOS 13.0, *)) [[TmSocialAuth shared] google:cid];
            else TmSend(@"google", @"", @"", @"iOS 13 gerekli");
        });
    }
}
