#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" bool LxyLauncherIconSet(const char* iconId)
{
    if (@available(iOS 10.3, *)) {
        if (![[UIApplication sharedApplication] supportsAlternateIcons]) {
            return false;
        }

        NSString* requestedId = iconId == NULL ? nil : [NSString stringWithUTF8String:iconId];
        NSString* alternateIconName = [requestedId isEqualToString:@"default"] ? nil : requestedId;
        dispatch_async(dispatch_get_main_queue(), ^{
            [[UIApplication sharedApplication] setAlternateIconName:alternateIconName
                                                   completionHandler:^(NSError* error) {
                if (error != nil) {
                    NSLog(@"[LauncherIcon] Failed to change alternate icon: %@", error);
                }
            }];
        });
        return true;
    }

    return false;
}
